using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LoadingModelMeshes
{
    internal static class Helpers
    {
        internal static void ToggleFullscreen(GraphicsDeviceManager _graphics, GraphicsDevice graphicsDevice, int windowedWidth, int windowedHeight)
        {
            if (_graphics.IsFullScreen)
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = windowedWidth;
                _graphics.PreferredBackBufferHeight = windowedHeight;
            }
            else
            {
                var display = graphicsDevice.Adapter.CurrentDisplayMode;
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = display.Width;
                _graphics.PreferredBackBufferHeight = display.Height;
            }
            _graphics.ApplyChanges();
        }
    }
}
