using System;
using ClassicUO.Configuration;
using ClassicUO.Network.Encryptions;
using ClassicUO.Network;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Scenes
{
    sealed class MainScene : Scene
    {
        private readonly GameController _controller;
        private readonly SpriteBatch _spritebatch;

        public MainScene(GameController controller)
        {
            _controller = controller;
            _spritebatch = new SpriteBatch(controller.GraphicsDevice);
        }

        public override void Update()
        {
            var keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.A))
            {
                _controller.UO.Load(_controller);
                Settings.GlobalSettings.Encryption = (byte)NetClient.Socket.Load(_controller.UO.FileManager.Version, (EncryptionType)Settings.GlobalSettings.Encryption);

                Log.Trace("Loading plugins...");
                _controller.PluginHost?.Initialize();

                foreach (string p in Settings.GlobalSettings.Plugins)
                {
                    Plugin.Create(p);
                }

                Log.Trace("Done!");

                _controller.SetScene(new LoginScene(_controller.UO.World));
            }

            base.Update();
        }

        public override bool Draw(UltimaBatcher2D batcher)
        {
            _spritebatch.Begin();
            _spritebatch.DrawRectangle(new Rectangle(0, 0, 100, 100), Color.Red);
            _spritebatch.End();
            return base.Draw(batcher);
        }

        public override void Load()
        {
            Console.WriteLine("MainScene loaded");
            base.Load();
        }

        public override void Unload()
        {
            Console.WriteLine("MainScene unloaded");
            base.Unload();
        }
    }
}