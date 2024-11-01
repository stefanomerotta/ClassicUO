using ClassicUO.Game;
using ClassicUO.Game.Data;
using System;
using Xunit;

namespace ClassicUO.UnitTests.Game.GameObjects.Effect
{
    public class Create
    {
        [Theory]
        [InlineData((int)GraphicEffectType.FixedXYZ, typeof(ClassicUO.Game.GameObjects.FixedEffect))]
        [InlineData((int)GraphicEffectType.FixedFrom, typeof(ClassicUO.Game.GameObjects.FixedEffect))]
        [InlineData((int)GraphicEffectType.DragEffect, typeof(ClassicUO.Game.GameObjects.DragEffect))]
        [InlineData((int)GraphicEffectType.Moving, typeof(ClassicUO.Game.GameObjects.MovingEffect))]
        [InlineData((int)GraphicEffectType.Lightning, typeof(ClassicUO.Game.GameObjects.LightningEffect))]
        public void Create_Returns_Effect_Instance(int graphicEffectType, Type type)
        {
            var world = new World();
            var em = new ClassicUO.Game.Managers.EffectManager(world);

            em.CreateEffect((GraphicEffectType) graphicEffectType, Serial.Zero, Serial.Zero, 1, 0,0, 0 , 0,0 ,0,0 ,0, 0, 
                false, false, false, GraphicEffectBlendMode.Normal);
            
            Assert.IsType(type, em.Items);

            em.Clear();
            world.Clear();
        }
    }
}
