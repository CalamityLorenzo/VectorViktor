using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Num = System.Numerics;

namespace Tools.DearImGui
{
    // Dear ImGui drawn by MonoGame. Dear ImGui is "immediate mode": each frame the program says what its panels hold
    // (ImGui.Begin, ImGui.SliderFloat(..., ref value), ImGui.End), straight from its own state, and Dear ImGui turns
    // that into triangles; there are no widget objects to keep in step. This is the half Dear ImGui leaves to each
    // engine: feeding it the mouse, keyboard and window size, and drawing its triangles.
    //
    // Each frame: BeginFrame, then any ImGui calls, then EndFrame (which draws them) - into the back buffer, after the
    // world. WantsMouse and WantsKeyboard say when the pointer is over a panel or a text box has the keys, so the
    // program leaves them alone.
    public sealed unsafe class ImGuiRenderer : IDisposable
    {
        // A vertex as Dear ImGui makes them (ImDrawVert: position, texture coordinate, colour; 20 bytes), described to
        // MonoGame. The colour's bytes are red, green, blue, alpha, as MonoGame's Color's are.
        private static readonly VertexDeclaration Declaration = new VertexDeclaration(sizeof(ImDrawVert),
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));

        // MonoGame's keys and the Dear ImGui keys they are, for the ones panels use: moving about text, editing it,
        // shortcuts, and every letter, digit and function key
        private static readonly (Keys key, ImGuiKey imgui)[] KeyMap = BuildKeyMap();

        private readonly GraphicsDevice _device;
        private readonly GameWindow _window;
        private readonly BasicEffect _effect;
        private readonly RasterizerState _rasterizer = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
        private readonly Dictionary<ulong, Texture2D> _textures = new Dictionary<ulong, Texture2D>();
        private readonly ImGuiContextPtr _context;
        private ulong _nextTexture = 1;
        private DynamicVertexBuffer? _vertices;
        private DynamicIndexBuffer? _indices;
        private byte[] _vertexBytes = Array.Empty<byte>();
        private byte[] _indexBytes = Array.Empty<byte>();
        private int _wheel;
        private KeyboardState _keyboard;

        // Whether Dear ImGui wants the mouse (it's over a panel, or dragging one) or the keyboard (a text box has it),
        // as of the last frame.
        public bool WantsMouse { get; private set; }
        public bool WantsKeyboard { get; private set; }

        public ImGuiRenderer(GraphicsDevice device, GameWindow window)
        {
            _device = device;
            _window = window;
            _effect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, World = Matrix.Identity, View = Matrix.Identity };
            _context = ImGui.CreateContext();
            var io = ImGui.GetIO();
            // This renderer makes Dear ImGui's textures when asked (1.92's way: see UpdateTextures), and can draw a
            // command's vertices from anywhere in the buffer (so big panels needn't fit 16-bit indices)
            io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures | ImGuiBackendFlags.RendererHasVtxOffset;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.IniFilename = null;   // don't save panel layouts beside the app
            window.TextInput += OnTextInput;
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (e.Character >= ' ' && e.Character != (char)127)
                ImGui.GetIO().AddInputCharacter(e.Character);
        }

        // Starts a frame: tells Dear ImGui the window's size, the time since the last frame, and what the mouse and keys
        // are doing. Then make ImGui calls, then EndFrame.
        public void BeginFrame(float seconds)
        {
            ImGui.SetCurrentContext(_context);
            var io = ImGui.GetIO();
            var back = _device.PresentationParameters;
            io.DisplaySize = new Num.Vector2(back.BackBufferWidth, back.BackBufferHeight);
            io.DeltaTime = seconds > 0f ? seconds : 1f / 60f;

            // Input goes in as events (what changed), which Dear ImGui plays through in order
            var mouse = Mouse.GetState(_window);
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
            io.AddMouseWheelEvent(0f, (mouse.ScrollWheelValue - _wheel) / 120f);
            _wheel = mouse.ScrollWheelValue;

            var keyboard = Keyboard.GetState();
            io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
            foreach (var (key, imgui) in KeyMap)
            {
                var down = keyboard.IsKeyDown(key);
                if (down != _keyboard.IsKeyDown(key))
                    io.AddKeyEvent(imgui, down);
            }
            _keyboard = keyboard;

            ImGui.NewFrame();
        }

        // Ends the frame and draws it, over whatever's in the render target (the back buffer, usually).
        public void EndFrame()
        {
            ImGui.Render();
            var io = ImGui.GetIO();
            WantsMouse = io.WantCaptureMouse;
            WantsKeyboard = io.WantCaptureKeyboard || io.WantTextInput;
            var data = ImGui.GetDrawData();
            UpdateTextures(data);
            Draw(data);
        }

        // Dear ImGui 1.92 grows its font texture as new characters and sizes are used, and asks the renderer to make,
        // update or drop its textures each frame. Here each is a Texture2D, known to Dear ImGui by a number.
        private void UpdateTextures(ImDrawDataPtr data)
        {
            var textures = data.Textures;
            for (var i = 0; i < textures.Size; i++)
            {
                var texture = textures.Data[i];
                switch (texture.Status)
                {
                    case ImTextureStatus.WantCreate:
                        var id = _nextTexture++;
                        _textures[id] = new Texture2D(_device, texture.Width, texture.Height, false, SurfaceFormat.Color);
                        Upload(texture, _textures[id], 0, 0, texture.Width, texture.Height);
                        texture.SetTexID(new ImTextureID(id));
                        texture.SetStatus(ImTextureStatus.Ok);
                        break;
                    case ImTextureStatus.WantUpdates:
                        var changed = texture.UpdateRect;   // the rectangle round every change since the last upload
                        Upload(texture, _textures[texture.TexID.Handle], changed.X, changed.Y, changed.W, changed.H);
                        texture.SetStatus(ImTextureStatus.Ok);
                        break;
                    case ImTextureStatus.WantDestroy when texture.UnusedFrames > 0:   // not while a frame in flight may use it
                        if (_textures.Remove(texture.TexID.Handle, out var gone))
                            gone.Dispose();
                        texture.SetTexID(ImTextureID.Null);
                        texture.SetStatus(ImTextureStatus.Destroyed);
                        break;
                }
            }
        }

        private static void Upload(ImTextureDataPtr texture, Texture2D target, int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;
            var pixels = new Color[width * height];
            var rgba = texture.Format == ImTextureFormat.Rgba32;
            for (var row = 0; row < height; row++)
            {
                var source = (byte*)texture.GetPixelsAt(x, y + row);
                for (var col = 0; col < width; col++)
                    pixels[row * width + col] = rgba
                        ? new Color(source[col * 4], source[col * 4 + 1], source[col * 4 + 2], source[col * 4 + 3])
                        : new Color((byte)255, (byte)255, (byte)255, source[col]);   // alpha only: white, that see-through
            }
            target.SetData(0, new Rectangle(x, y, width, height), pixels, 0, pixels.Length);
        }

        // Every draw list's vertices and indices into one pair of buffers, then each command drawn: its triangles, its
        // texture, clipped to its rectangle.
        private void Draw(ImDrawDataPtr data)
        {
            if (data.TotalVtxCount == 0)
                return;
            var vertexBytes = data.TotalVtxCount * sizeof(ImDrawVert);
            var indexBytes = data.TotalIdxCount * sizeof(ushort);
            if (_vertexBytes.Length < vertexBytes)
            {
                _vertexBytes = new byte[vertexBytes * 2];   // room to grow, so it isn't remade every frame
                _vertices?.Dispose();
                _vertices = new DynamicVertexBuffer(_device, Declaration, data.TotalVtxCount * 2, BufferUsage.WriteOnly);
            }
            if (_indexBytes.Length < indexBytes)
            {
                _indexBytes = new byte[indexBytes * 2];
                _indices?.Dispose();
                _indices = new DynamicIndexBuffer(_device, IndexElementSize.SixteenBits, data.TotalIdxCount * 2, BufferUsage.WriteOnly);
            }
            int vertexAt = 0, indexAt = 0;
            for (var n = 0; n < data.CmdLists.Size; n++)
            {
                var list = data.CmdLists.Data[n];
                var (vertices, indices) = (list.VtxBuffer.Size * sizeof(ImDrawVert), list.IdxBuffer.Size * sizeof(ushort));
                fixed (byte* to = &_vertexBytes[vertexAt])
                    Buffer.MemoryCopy(list.VtxBuffer.Data, to, _vertexBytes.Length - vertexAt, vertices);
                fixed (byte* to = &_indexBytes[indexAt])
                    Buffer.MemoryCopy(list.IdxBuffer.Data, to, _indexBytes.Length - indexAt, indices);
                vertexAt += vertices;
                indexAt += indices;
            }
            _vertices!.SetData(0, _vertexBytes, 0, vertexBytes, 1, SetDataOptions.Discard);
            _indices!.SetData(_indexBytes, 0, indexBytes, SetDataOptions.Discard);

            // Pixels, top left at (0, 0), y down; see-through edges blended over what's there; no depth
            var viewport = _device.Viewport;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, -1f, 1f);
            _device.SetVertexBuffer(_vertices);
            _device.Indices = _indices;
            _device.BlendState = BlendState.NonPremultiplied;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = _rasterizer;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            var origin = data.DisplayPos;
            int baseVertex = 0, baseIndex = 0;
            for (var n = 0; n < data.CmdLists.Size; n++)
            {
                var list = data.CmdLists.Data[n];
                for (var c = 0; c < list.CmdBuffer.Size; c++)
                {
                    var command = list.CmdBuffer.Data[c];
                    if (command.ElemCount == 0 || !_textures.TryGetValue(command.GetTexID().Handle, out var texture))
                        continue;
                    var clip = command.ClipRect;   // left, top, right, bottom
                    var scissor = new Rectangle((int)(clip.X - origin.X), (int)(clip.Y - origin.Y), (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                    if (scissor.Width <= 0 || scissor.Height <= 0)
                        continue;
                    _device.ScissorRectangle = scissor;
                    _effect.Texture = texture;
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, baseVertex + (int)command.VtxOffset,
                            baseIndex + (int)command.IdxOffset, (int)command.ElemCount / 3);
                    }
                }
                baseVertex += list.VtxBuffer.Size;
                baseIndex += list.IdxBuffer.Size;
            }
        }

        private static (Keys, ImGuiKey)[] BuildKeyMap()
        {
            var map = new List<(Keys, ImGuiKey)>
            {
                (Keys.Tab, ImGuiKey.Tab), (Keys.Left, ImGuiKey.LeftArrow), (Keys.Right, ImGuiKey.RightArrow),
                (Keys.Up, ImGuiKey.UpArrow), (Keys.Down, ImGuiKey.DownArrow), (Keys.PageUp, ImGuiKey.PageUp),
                (Keys.PageDown, ImGuiKey.PageDown), (Keys.Home, ImGuiKey.Home), (Keys.End, ImGuiKey.End),
                (Keys.Insert, ImGuiKey.Insert), (Keys.Delete, ImGuiKey.Delete), (Keys.Back, ImGuiKey.Backspace),
                (Keys.Space, ImGuiKey.Space), (Keys.Enter, ImGuiKey.Enter), (Keys.Escape, ImGuiKey.Escape),
                (Keys.LeftControl, ImGuiKey.LeftCtrl), (Keys.RightControl, ImGuiKey.RightCtrl),
                (Keys.LeftShift, ImGuiKey.LeftShift), (Keys.RightShift, ImGuiKey.RightShift),
                (Keys.LeftAlt, ImGuiKey.LeftAlt), (Keys.RightAlt, ImGuiKey.RightAlt),
                (Keys.OemMinus, ImGuiKey.Minus), (Keys.OemPlus, ImGuiKey.Equal), (Keys.OemPeriod, ImGuiKey.Period),
                (Keys.OemComma, ImGuiKey.Comma), (Keys.Subtract, ImGuiKey.KeypadSubtract), (Keys.Add, ImGuiKey.KeypadAdd),
            };
            for (var k = 0; k < 26; k++)
                map.Add((Keys.A + k, ImGuiKey.A + k));
            for (var k = 0; k < 10; k++)
            {
                map.Add((Keys.D0 + k, ImGuiKey.Key0 + k));
                map.Add((Keys.NumPad0 + k, ImGuiKey.Keypad0 + k));
            }
            for (var k = 0; k < 12; k++)
                map.Add((Keys.F1 + k, ImGuiKey.F1 + k));
            return map.ToArray();
        }

        public void Dispose()
        {
            _window.TextInput -= OnTextInput;
            ImGui.DestroyContext(_context);
            foreach (var texture in _textures.Values)
                texture.Dispose();
            _vertices?.Dispose();
            _indices?.Dispose();
            _effect.Dispose();
            _rasterizer.Dispose();
        }
    }
}
