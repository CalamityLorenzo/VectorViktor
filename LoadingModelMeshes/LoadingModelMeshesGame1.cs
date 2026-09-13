using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LoadingModelMeshes
{
    public class LoadingModelMeshesGame1 : Game
    {
        private Color _backgroundColour = new Color(27, 13, 120); // deep blue-purple
        private Color _wireColour = new Color(60, 255, 120); // phosphor green
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Pass 1 fills every instance in the background colour (writing depth only, no visible
        // shading), pass 2 draws every instance's hard edges on top with a depth test. Running
        // each pass across the whole scene (rather than per-instance) means a nearer object's
        // depth correctly hides a farther object's edges too, not just its own - giving the
        // hidden-line-removal "solid vector" look of 1980s wireframe games like Elite,
        // Mercenary and Tau Ceti instead of a see-through cage.
        private RasterizerState _fillRasterizerState;
        private BasicEffect _edgeEffect;

        // Models placed in the scene. Several instances can point at the same loaded Model -
        // its hard-edge cache is only built once, in GetOrBuildRenderData.
        private readonly List<WireframeModel> _sceneModels = new List<WireframeModel>();
        private readonly Dictionary<Model, ModelRenderData> _renderDataByModel = new Dictionary<Model, ModelRenderData>();

        private KeyboardState _prevKeys;
        private MouseState _prevMouse;

        private Matrix _view;
        private Matrix _projection;
        private bool _isRotating = true;

        // Orbit camera state: spherical coordinates around the scene's combined bounding sphere.
        private Vector3 _cameraTarget;
        private float _cameraYaw;
        private float _cameraPitch;
        private float _cameraDistance;
        private float _minCameraDistance;
        private float _maxCameraDistance;

        // Windowed size to restore when leaving fullscreen
        private const int WindowedWidth = 1280;
        private const int WindowedHeight = 720;

        private sealed class ModelRenderData
        {
            public Dictionary<ModelMesh, VertexPosition[]> EdgeVerticesByMesh;
            public BoundingSphere LocalBounds;
        }

        public LoadingModelMeshesGame1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = WindowedWidth,
                PreferredBackBufferHeight = WindowedHeight,
                PreferMultiSampling = false,
                HardwareModeSwitch = false,   // borderless fullscreen, no display mode change
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _fillRasterizerState = new RasterizerState
            {
                CullMode = CullMode.CullCounterClockwiseFace,
                FillMode = FillMode.Solid
            };

            _edgeEffect = new BasicEffect(GraphicsDevice)
            {
                LightingEnabled = false,
                TextureEnabled = false,
                VertexColorEnabled = false
            };

            // Add every model the scene should contain here. Instances may reuse the same
            // Content.Load<Model>(...) call - each unique asset's edge cache and bounds are
            // only computed once, in GetOrBuildRenderData.
            Model carModel = Content.Load<Model>("EastGermanCar");
            float spacing = WireframeGeometry.ComputeLocalBounds(carModel).Radius * 3f;
            AddModelInstance(carModel, new Vector3(-spacing, 0, 0));
            AddModelInstance(carModel, Vector3.Zero);
            AddModelInstance(carModel, new Vector3(spacing, 0, 0));

            // Frame the camera from the whole scene's bounding sphere instead of a guessed
            // distance, since FBX export scale (cm vs m) varies per asset.
            BoundingSphere sceneBounds = ComputeSceneBounds();
            _cameraTarget = sceneBounds.Center;
            _cameraYaw = 0f;
            _cameraPitch = 0.2f;
            _cameraDistance = sceneBounds.Radius * 2.5f;
            _minCameraDistance = sceneBounds.Radius * 0.6f;
            _maxCameraDistance = sceneBounds.Radius * 10f;

            _projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4,
                GraphicsDevice.Viewport.AspectRatio,
                System.Math.Max(0.1f, sceneBounds.Radius * 0.01f),
                System.Math.Max(1000f, sceneBounds.Radius * 10f));

            _prevMouse = Mouse.GetState();
            UpdateViewMatrix();
        }

        // Adds a placed instance of a model to the scene, building (and caching) its hard-edge
        // data the first time this Model asset is seen.
        private WireframeModel AddModelInstance(Model model, Vector3 position)
        {
            if (!_renderDataByModel.TryGetValue(model, out ModelRenderData data))
            {
                data = new ModelRenderData
                {
                    EdgeVerticesByMesh = WireframeGeometry.BuildHardEdgeVertices(model),
                    LocalBounds = WireframeGeometry.ComputeLocalBounds(model)
                };
                _renderDataByModel[model] = data;
            }

            var instance = new WireframeModel(model, data.EdgeVerticesByMesh, data.LocalBounds, position);
            _sceneModels.Add(instance);
            return instance;
        }

        private BoundingSphere ComputeSceneBounds()
        {
            BoundingSphere sphere = new BoundingSphere(Vector3.Zero, 0);
            foreach (WireframeModel instance in _sceneModels)
                sphere = BoundingSphere.CreateMerged(sphere, instance.GetWorldBounds());
            return sphere;
        }

        private void UpdateViewMatrix()
        {
            Vector3 offset = new Vector3(
                _cameraDistance * (float)System.Math.Cos(_cameraPitch) * (float)System.Math.Sin(_cameraYaw),
                _cameraDistance * (float)System.Math.Sin(_cameraPitch),
                _cameraDistance * (float)System.Math.Cos(_cameraPitch) * (float)System.Math.Cos(_cameraYaw));

            _view = Matrix.CreateLookAt(_cameraTarget + offset, _cameraTarget, Vector3.Up);
        }

        protected override void Update(GameTime gameTime)
        {
            var keys = Keyboard.GetState();
            var mouse = Mouse.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
            // F11 or Alt+Enter toggles fullscreen
            bool altEnter = keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter)
                && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            if ((keys.IsKeyDown(Keys.F11) && _prevKeys.IsKeyUp(Keys.F11)) || altEnter)
                Helpers.ToggleFullscreen(_graphics, GraphicsDevice, WindowedWidth, WindowedHeight);

            // Space toggles auto-rotation of every model in the scene.
            if (keys.IsKeyDown(Keys.Space) && _prevKeys.IsKeyUp(Keys.Space))
                _isRotating = !_isRotating;

            if (_isRotating)
            {
                float rotationDelta = (float)gameTime.ElapsedGameTime.TotalSeconds * MathHelper.PiOver4 * 0.5f;
                foreach (WireframeModel instance in _sceneModels)
                    instance.RotationY += rotationDelta;
            }

            Vector2 mouseDelta = new Vector2(mouse.X - _prevMouse.X, mouse.Y - _prevMouse.Y);
            bool shiftHeld = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            bool cameraChanged = false;

            // Shift + drag (either mouse button) orbits the camera around the scene.
            if (shiftHeld && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
            {
                const float orbitSpeed = 0.005f;
                _cameraYaw -= mouseDelta.X * orbitSpeed;
                _cameraPitch -= mouseDelta.Y * orbitSpeed;
                _cameraPitch = MathHelper.Clamp(_cameraPitch, -MathHelper.PiOver2 + 0.05f, MathHelper.PiOver2 - 0.05f);
                cameraChanged = true;
            }

            // Scroll wheel zooms in/out: scroll forward (away from you) to zoom in, back to zoom out.
            int scrollDelta = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                const float zoomSpeed = 0.0015f;
                _cameraDistance -= scrollDelta * zoomSpeed * _cameraDistance;
                _cameraDistance = MathHelper.Clamp(_cameraDistance, _minCameraDistance, _maxCameraDistance);
                cameraChanged = true;
            }

            if (cameraChanged)
                UpdateViewMatrix();

            _prevKeys = keys;
            _prevMouse = mouse;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColour);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            DrawScene();

            base.Draw(gameTime);
        }

        private void DrawScene()
        {
            // Compute each instance's world matrix and bone transforms once per frame, and
            // reuse them for both passes below.
            var frame = new List<(WireframeModel Instance, Matrix World, Matrix[] BoneTransforms)>(_sceneModels.Count);
            foreach (WireframeModel instance in _sceneModels)
            {
                Matrix world = instance.GetWorldMatrix();
                var boneTransforms = new Matrix[instance.Model.Bones.Count];
                instance.Model.CopyAbsoluteBoneTransformsTo(boneTransforms);
                frame.Add((instance, world, boneTransforms));
            }

            // Pass 1: fill every instance in the background colour across the whole scene, so
            // hidden edges - including edges hidden by a *different* instance - get occluded.
            GraphicsDevice.RasterizerState = _fillRasterizerState;
            foreach (var entry in frame)
                DrawMeshes(entry.Instance.Model, entry.BoneTransforms, entry.World, _view, _projection, _backgroundColour);

            // Pass 2: only the visible hard edges survive the depth test written in pass 1.
            _edgeEffect.View = _view;
            _edgeEffect.Projection = _projection;
            _edgeEffect.DiffuseColor = _wireColour.ToVector3();
            foreach (var entry in frame)
                DrawHardEdges(entry.Instance.Model, entry.Instance.EdgeVerticesByMesh, entry.BoneTransforms, entry.World);
        }

        private void DrawHardEdges(Model model, IReadOnlyDictionary<ModelMesh, VertexPosition[]> edgeVerticesByMesh, Matrix[] boneTransforms, Matrix world)
        {
            foreach (ModelMesh mesh in model.Meshes)
            {
                VertexPosition[] vertices = edgeVerticesByMesh[mesh];
                if (vertices.Length == 0)
                    continue;

                _edgeEffect.World = boneTransforms[mesh.ParentBone.Index] * world;

                foreach (EffectPass pass in _edgeEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
                }
            }
        }

        private static void DrawMeshes(Model model, Matrix[] boneTransforms, Matrix world, Matrix view, Matrix projection, Color color)
        {
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    // The FBX has no normal data, so default lighting (which needs NORMAL0)
                    // would throw a vertex declaration mismatch. Render unlit instead.
                    effect.LightingEnabled = false;
                    effect.TextureEnabled = false;
                    effect.VertexColorEnabled = false;
                    effect.DiffuseColor = color.ToVector3();
                    effect.World = boneTransforms[mesh.ParentBone.Index] * world;
                    effect.View = view;
                    effect.Projection = projection;
                }

                mesh.Draw();
            }
        }

        protected override void UnloadContent()
        {
            _fillRasterizerState?.Dispose();
            _edgeEffect?.Dispose();
            base.UnloadContent();
        }
    }
}
