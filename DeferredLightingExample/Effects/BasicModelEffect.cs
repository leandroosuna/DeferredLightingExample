using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using DeferredLightingExample.Managers;

namespace DeferredLightingExample.Effects
{
    public class BasicModelEffect
    {
        public Effect effect;
        EffectParameter world;
        EffectParameter view;
        EffectParameter projection;

        EffectParameter color;
        EffectParameter colorTexture;
        EffectParameter inverseTransposeWorld;
        EffectParameter lightAmbientColor;
        EffectParameter lightDiffuseColor;
        EffectParameter lightSpecularColor;
        EffectParameter KA;
        EffectParameter KD;
        EffectParameter KS;
        EffectParameter shininess;
        EffectParameter cameraPosition;
        EffectParameter lightPosition;

        EffectParameter tiling;

        public BasicModelEffect(string effect)
        {
            this.effect = DeferredGame.GetInstance().Content.Load<Effect>(DeferredGame.ContentFolderEffects + effect);

            cacheParams();
            
        }
        void cacheParams()
        {
            world = effect.Parameters["world"];
            view = effect.Parameters["view"];
            projection = effect.Parameters["projection"];
            colorTexture = effect.Parameters["colorTexture"];
            color = effect.Parameters["color"];
            inverseTransposeWorld = effect.Parameters["inverseTransposeWorld"];
            cameraPosition = effect.Parameters["cameraPosition"];
            
            lightPosition = effect.Parameters["lightPosition"];
            lightAmbientColor = effect.Parameters["lightAmbientColor"];
            lightDiffuseColor = effect.Parameters["lightDiffuseColor"];
            lightSpecularColor = effect.Parameters["lightSpecularColor"];
            KA = effect.Parameters["KA"];
            KD = effect.Parameters["KD"];
            KS = effect.Parameters["KS"];
            shininess = effect.Parameters["shininess"];
            tiling = effect.Parameters["tiling"];
        }
        public void SetWorld(Matrix world)
        {
            this.world.SetValue(world);
        }
        public void SetView(Matrix view)
        {
            this.view.SetValue(view);
        }
        public void SetProjection(Matrix projection)
        {
            this.projection.SetValue(projection);
        }
        public void SetTiling(Vector2 tiling)
        {
            this.tiling.SetValue(tiling);
        }
        public void SetColorTexture(Texture2D texture)
        {
            this.colorTexture.SetValue(texture);
        }
        public void SetColor(Vector3 color)
        {
            this.color.SetValue(color);
        }
        public void SetTech(string tech)
        {
            if(effect.CurrentTechnique != effect.Techniques[tech])
                effect.CurrentTechnique = effect.Techniques[tech];
        }
        public void SetInverseTransposeWorld(Matrix itw)
        {
            inverseTransposeWorld.SetValue(itw);
        }
        public void SetLightPosition(Vector3 pos)
        {
            lightPosition.SetValue(pos);
        }
        public void SetCameraPosition(Vector3 pos)
        {
            cameraPosition.SetValue(pos);
        }
        public void SetLightAmbientColor(Vector3 color)
        {
            lightAmbientColor.SetValue(color); 
        }
        public void SetLightDiffuseColor(Vector3 color)
        {
            lightDiffuseColor.SetValue(color);
        }
        public void SetLightSpecularColor(Vector3 color)
        { 
            lightSpecularColor.SetValue(color);
        }
        public void SetKA(float value)
        {
            KA.SetValue(value);
        }
        public void SetKD(float value)
        {
            KD.SetValue(value);
        }
        public void SetKS(float value)
        {
            KS.SetValue(value);
        }
        public void SetShininess(float value)
        {
            shininess.SetValue(value);
        }
        public void SetAmbientLight(AmbientLight ambient)
        {
            SetLightDiffuseColor(ambient.color);
            SetLightAmbientColor(ambient.ambientColor);
            SetLightPosition(ambient.position);
            SetLightSpecularColor(ambient.specularColor);
        }
    }
}
