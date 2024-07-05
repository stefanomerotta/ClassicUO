using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Utility;
using Microsoft.Xna.Framework.Graphics;
using TinyEcs;

namespace ClassicUO.Ecs;


struct AssetsServer
{
    public Renderer.Arts.Art Arts;
    public Renderer.Animations.Animations Animations;
    public Renderer.Texmaps.Texmap Texmaps;
    public Renderer.Gumps.Gump Gumps;
}

readonly struct AssetsPlugin : IPlugin
{
    public unsafe void Build(Scheduler scheduler)
    {
        scheduler.AddSystem((
            Res<GraphicsDevice> device,
            Res<GameContext> gameCtx,
            Res<Settings> settings,
            SchedulerState schedState
        ) => {
            Assets.UOFileManager.Load(gameCtx.Value.ClientVersion, settings.Value.UltimaOnlineDirectory, false, settings.Value.Language);

            schedState.AddResource(Assets.TileDataLoader.Instance);
            schedState.AddResource(Assets.MapLoader.Instance);
            schedState.AddResource(new AssetsServer()
            {
                Arts = new Renderer.Arts.Art(device),
                Texmaps = new Renderer.Texmaps.Texmap(device),
                Animations = new Renderer.Animations.Animations(device),
                Gumps = new Renderer.Gumps.Gump(device)
            });
            schedState.AddResource(new Renderer.UltimaBatcher2D(device));

            gameCtx.Value.Protocol = ClientFlags.CF_T2A;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_200)
                gameCtx.Value.Protocol |= ClientFlags.CF_RE;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_300)
                gameCtx.Value.Protocol |= ClientFlags.CF_TD;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_308)
                gameCtx.Value.Protocol |= ClientFlags.CF_LBR;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_308Z)
                gameCtx.Value.Protocol |= ClientFlags.CF_AOS;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_405A)
                gameCtx.Value.Protocol |= ClientFlags.CF_SE;

            if (gameCtx.Value.ClientVersion >= ClientVersion.CV_60144)
                gameCtx.Value.Protocol |= ClientFlags.CF_SA;


            const int TEXTURE_WIDTH = 512;
            const int TEXTURE_HEIGHT = 1024;
            const int LIGHTS_TEXTURE_WIDTH = 32;
            const int LIGHTS_TEXTURE_HEIGHT = 63;

            var hueSamplers = new Texture2D[2];
            hueSamplers[0] = new Texture2D(device, TEXTURE_WIDTH, TEXTURE_HEIGHT);
            hueSamplers[1] = new Texture2D(device, LIGHTS_TEXTURE_WIDTH, LIGHTS_TEXTURE_HEIGHT);

            var buffer = new uint[Math.Max(
                LIGHTS_TEXTURE_WIDTH * LIGHTS_TEXTURE_HEIGHT,
                TEXTURE_WIDTH * TEXTURE_HEIGHT
            )];

            Assets.HuesLoader.Instance.CreateShaderColors(buffer);
            fixed (uint* ptr = buffer)
            {
                hueSamplers[0].SetDataPointerEXT(
                    0,
                    null,
                    (IntPtr)ptr,
                    TEXTURE_WIDTH * TEXTURE_HEIGHT * sizeof(uint)
                );

                LightColors.CreateLightTextures(buffer, LIGHTS_TEXTURE_HEIGHT);
                hueSamplers[1].SetDataPointerEXT(
                    0,
                    null,
                    (IntPtr)ptr,
                    LIGHTS_TEXTURE_WIDTH * LIGHTS_TEXTURE_HEIGHT * sizeof(uint)
                );
            }

            device.Value.Textures[1] = hueSamplers[0];
            device.Value.Textures[2] = hueSamplers[1];
        }, Stages.Startup);
    }
}