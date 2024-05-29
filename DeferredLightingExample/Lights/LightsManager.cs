using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DeferredLightingExample.Effects;

namespace DeferredLightingExample.Managers
{
    public class LightsManager
    {
        DeferredGame game;
        DeferredEffect effect;

        public List<LightVolume> lights = new List<LightVolume>();

        public List<LightVolume> lightsToDraw = new List<LightVolume>();

        public AmbientLight ambientLight;
        public LightsManager()
        {
            game = DeferredGame.GetInstance();
            effect = game.deferredEffect;
            effect.SetScreenSize(new Vector2(game.screenWidth, game.screenHeight));
        }
        //float ang = 0;
        public void Update(float deltaTime)
        {
            lightsToDraw.Clear();
            
            foreach (var l in lights)
            {
                l.Update();

                if(l.enabled && game.camera.frustumContains(l.collider))
                    lightsToDraw.Add(l);
            }
        }
        public void Draw()
        {

            DrawAmbient();

            //effect.SetTech("point_light");
            game.GraphicsDevice.RasterizerState = RasterizerState.CullClockwise; //remove front side of spheres to be drawn

            lightsToDraw.ForEach(l => l.Draw());
            
        }
        public void DrawAmbient()
        {
            effect.SetCameraPosition(game.camera.position);
            effect.SetView(game.camera.view);
            effect.SetProjection(game.camera.projection);

            effect.SetAmbientLight(ambientLight);

            effect.SetTech("ambient_light");

            game.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            game.fullScreenQuad.Draw(effect.effect);
        }
        public int partialLightsCount = 0;
        public void DrawLightPartial()
        {
            //effect.SetTech("point_light");
            game.GraphicsDevice.RasterizerState = RasterizerState.CullClockwise; //remove front side of spheres to be drawn

            for (int i = 0; i < partialLightsCount; i++)
            {
                lightsToDraw[i].Draw();
            }
        }
        public void DrawLightGeo()
        {
            lightsToDraw.ForEach(l => l.DrawLightGeo());            
        }

        public void DrawLightGeoPartial()
        {

            for (int i = 0; i < partialLightsCount; i++)
            {
                lightsToDraw[i].DrawLightGeo();
            }

        }

        public void register(LightVolume volume)
        { 
            lights.Add(volume);
        }
        public void destroy(LightVolume volume)
        {
            lights.Remove(volume);
        }
    }
}
