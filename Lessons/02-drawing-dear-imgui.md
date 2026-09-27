# 02: Drawing Dear ImGui with MonoGame

**What you'll learn:** what "immediate-mode" user interface means; what Dear ImGui does and what it leaves to each engine; how a renderer feeds it input and draws what it hands back (vertex and index buffers, clipping, textures); and what it's like to use a C library from C#.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) step 2, as [Tools.DearImGui](../Tools.DearImGui/ImGuiRenderer.cs).

## The problem

The playground and the map studio need panels: sliders for an experiment's settings, lists of starts, plots of how the droid's lean changes. A game's own screens are usually drawn by hand. Tools want many controls quickly and don't need them to be beautiful.

## The idea: immediate mode

Most UI toolkits (Windows Forms, WPF, the web's DOM) are **retained mode**: you create widget *objects* (a slider, a label), keep them, and keep them in step with your data, copying values in and out and handling events.

**Immediate mode** throws the objects away. Every frame, the program *describes* its panels from its own state, and the library draws them:

```csharp
ImGui.Begin("Segway");
ImGui.SliderFloat("gain", ref Gain, 0.02f, 1f);   // draws the slider AND changes Gain if it's dragged
if (ImGui.Button("Defaults"))                      // true on the frame it's clicked
    Defaults();
ImGui.End();
```

There is no slider object. `Gain` is the only copy of the value, so it can't get out of step. A panel that shouldn't show just isn't described that frame. This suits tools, where the data changes all the time and the panels are simple.

**Dear ImGui** is the best-known immediate-mode library: C++, used across the games industry for debug tools. **Hexa.NET.ImGui** wraps it for .NET, generated from its C interface (cimgui), and keeps up with its releases. That's why it's used here rather than ImGui.NET, which stopped at 1.91.6. We use the experimental 3.1.0 (Dear ImGui 1.92.9b), **pinned** in [Directory.Packages.props](../Directory.Packages.props), so it changes only when we choose.

## What Dear ImGui leaves to us

Dear ImGui doesn't know about windows, keyboards or graphics cards. Each engine supplies a **backend** in two halves:

| Half | Job | In [ImGuiRenderer](../Tools.DearImGui/ImGuiRenderer.cs) |
|---|---|---|
| Platform | the window's size, the time, the mouse and keys | `BeginFrame` |
| Renderer | make its textures; draw its triangles | `UpdateTextures`, `Draw` |

Dear ImGui ships backends for DirectX, OpenGL, Vulkan and others, but none for MonoGame, so this one is ours, about 250 lines.

### Input goes in as events

```csharp
io.AddMousePosEvent(mouse.X, mouse.Y);
io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
io.AddKeyEvent(imguiKey, down);   // only when a key changes
```

Dear ImGui takes *what changed*, not *what's true now*, and plays the changes through in order, so a click that goes down and up between two frames isn't lost. Typed characters come from MonoGame's `Window.TextInput` event, which already handles Shift, keyboard layouts and accents.

### Textures: the 1.92 way

Dear ImGui draws text from a **font atlas**: one texture with every character it has needed. Since 1.92 the atlas **grows as it goes**: a new character or size is added when it's first used. So each frame the renderer checks a list of textures, each marked `WantCreate`, `WantUpdates` (a rectangle has changed) or `WantDestroy`. It makes a `Texture2D`, uploads just the changed rectangle, or disposes it, then tells Dear ImGui it's done. It also waits one frame before destroying (`UnusedFrames > 0`), in case a frame still in flight uses the texture.

### Drawing: vertices, indices, commands

`ImGui.Render()` hands back **draw lists**. Each has:
- **vertices**: a position, a texture coordinate and a colour, 20 bytes each (`ImDrawVert`). A `VertexDeclaration` describes the same layout to MonoGame, so the bytes are copied straight across without converting.
- **indices**: 16-bit numbers, three per triangle, saying which vertices make it. Corners shared by two triangles are stored once.
- **commands**: "draw these many indices, from here, with this texture, clipped to this rectangle".

The renderer copies every list into one dynamic vertex buffer and one index buffer, then draws each command:

- **An orthographic projection in pixels:** (0, 0) is the top left, and y goes down, as Dear ImGui thinks.
- **A scissor rectangle** clips each command to its panel. That's how a scrolled list stays inside its window.
- **Alpha blending, no depth test, no culling:** the panels are flat and drawn in order over the world.
- **A base vertex** (`RendererHasVtxOffset`): a command can start anywhere in the big buffer, so large panels aren't limited to 65,536 vertices.

### Where it fits in a frame

The world is drawn small (480 × 270) and scaled up with hard pixels ([RetroGame](../MeshRendering/RetroGame.cs)). Panels drawn in the small picture would be blurry blocks, so `RetroGame` got a **`DrawOverlay`** step. It's drawn after the scale-up, at the window's full resolution, and that's where the playground draws its panels.

One more hook: while a text box has the keyboard, pressing C shouldn't toggle the world's colours. The renderer reports `WantsKeyboard` and `WantsMouse`; the playground passes them on through `RetroGame.KeyboardCaptured`, and ignores the mouse over a panel.

## A C library from C#

Hexa's API is generated from C, so it shows through:
- **`unsafe` and pointers.** `list.VtxBuffer.Data` is an `ImDrawVert*`, and `Buffer.MemoryCopy` copies from it. The renderer's project turns on `AllowUnsafeBlocks`.
- **`...Ptr` structs** (`ImDrawDataPtr`, `ImTextureDataPtr`) wrap a pointer and make its fields look like properties.
- **Several overloads of everything:** C# `string`, UTF-8 `ReadOnlySpan<byte>`, raw `byte*`. Use the string ones; the others save a conversion, for hot loops.
- **A native DLL**, `cimgui.dll`, comes in the package (`runtimes/win-x64/native`) and is copied beside the app.

Because it's close to C++, Dear ImGui's own documentation and its `imgui_demo.cpp` translate almost line for line.

## Things that went wrong on the way

From the spike (a throwaway test app) that proved this would work:
- **`Color` is ambiguous.** Implicit `using`s plus Windows Forms bring in `System.Drawing.Color` beside MonoGame's. The fix: don't turn on Windows Forms where it isn't needed.
- **"Cannot call Present when a render target is active".** A frame must end drawing to the back buffer. The spike left a render target set.

## Exercise (optional): a Stats window

Add a panel to the playground that plots the draw calls each frame, and a checkbox that opens Dear ImGui's own demo window, the best tour of every widget there is.

1. In [Playground.cs](../Droid.Playground/Playground.cs), add fields: a `Trace` for the draw calls (see [Trace.cs](../Droid.Playground/Trace.cs)) and a `bool` for the demo window.
2. In `DrawOverlay`, before `_imgui.EndFrame()`:
   - put the window somewhere clear with `ImGui.SetNextWindowPos(new Num.Vector2(10, 470), ImGuiCond.FirstUseEver)`;
   - `ImGui.Begin("Stats")`;
   - add `_renderer.Batch.DrawCalls` to your trace and `Plot` it (0 to 1000);
   - `ImGui.Checkbox("Dear ImGui demo", ref yourBool)`;
   - `ImGui.End()`;
   - then, if the bool is set, `ImGui.ShowDemoWindow(ref yourBool)`. Passing it by `ref` gives the demo window a close button that clears it.

**Check it:** run `Droid.Playground`, drive about and watch the plot move; open the demo, try its widgets, and close it with its ×.

**Stretch:** in the demo, open "Tools > Style Editor" and try a style you like. The playground could set it at start with `ImGui.StyleColorsLight()` or by changing `ImGui.GetStyle()`.

## Thinking questions

1. Immediate mode redraws every panel every frame. Why is that cheap enough, when a retained-mode toolkit works hard to redraw only what changed?
2. Why does the renderer wait for `UnusedFrames > 0` before destroying a texture?
3. The panels are drawn after the world's picture is scaled up. What would they look like drawn into the 480 × 270 picture, and why?

<details>
<summary>Answers</summary>

1. A game already redraws its whole screen 60 times a second, so a few thousand triangles more is nothing, and Dear ImGui batches them into a handful of draw calls. What retained toolkits save is *layout and bookkeeping* for complex, mostly still screens; tools rarely need that.
2. The graphics card works a frame or two behind the program. A texture used in the last frame's commands may not have been drawn from yet, so destroying it at once could pull it out from under the card.
3. Blocky and hard to read. Every pixel would be scaled up three times, and at 480 × 270 there aren't enough pixels to draw small text at all. The retro look is for the droid's view of the world, not for tools.

</details>

## Further reading

- Dear ImGui's [README](https://github.com/ocornut/imgui) and its FAQ, especially "How can I tell whether to dispatch mouse/keyboard to Dear ImGui or my application?" (that's `WantsMouse` and `WantsKeyboard`).
- Casey Muratori's talk *Immediate-Mode Graphical User Interfaces* (2005), where the idea got its name.
- `imgui_demo.cpp` in Dear ImGui's source: every widget, with the code beside it.
