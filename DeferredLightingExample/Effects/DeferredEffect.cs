using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using DeferredLightingExample.Managers;

namespace DeferredLightingExample.Effects
{
    public class DeferredEffect
    {
        public Effect effect;
        EffectParameter world;
        EffectParameter view;
        EffectParameter projection;
        EffectParameter colorMap;
        EffectParameter normalMap;
        EffectParameter positionMap;
        EffectParameter lightMap;
        EffectParameter bloomFilter;
        EffectParameter blurH;
        EffectParameter blurV;


        EffectParameter screenSize;
        EffectParameter radius;
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


        public DeferredEffect(string effect)
        {
            
            this.effect = DeferredGame.GetInstance().Content.Load<Effect>(DeferredGame.ContentFolderEffects + effect);

            cacheParams();
            
        }
        void cacheParams()
        {
            world = effect.Parameters["world"];
            view = effect.Parameters["view"];
            projection = effect.Parameters["projection"];

            colorMap = effect.Parameters["colorMap"];
            normalMap = effect.Parameters["normalMap"];
            positionMap = effect.Parameters["positionMap"];
            lightMap = effect.Parameters["lightMap"];


            inverseTransposeWorld = effect.Parameters["inverseTransposeWorld"];
            
            cameraPosition = effect.Parameters["cameraPosition"];
            screenSize = effect.Parameters["screenSize"];

            lightPosition = effect.Parameters["lightPosition"];
            radius = effect.Parameters["radius"];
            lightAmbientColor = effect.Parameters["lightAmbientColor"];
            lightDiffuseColor = effect.Parameters["lightDiffuseColor"];
            lightSpecularColor = effect.Parameters["lightSpecularColor"];
            KA = effect.Parameters["KA"];
            KD = effect.Parameters["KD"];
            KS = effect.Parameters["KS"];
            shininess = effect.Parameters["shininess"];

            bloomFilter = effect.Parameters["bloomFilter"];
            blurH = effect.Parameters["blurH"];
            blurV = effect.Parameters["blurV"];

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
        public void SetColorMap(Texture2D texture)
        {
            colorMap.SetValue(texture);
        }
        public void SetNormalMap(Texture2D texture)
        {
            normalMap.SetValue(texture);
        }
        public void SetPositionMap(Texture2D texture)
        {
            positionMap.SetValue(texture);
        }
        public void SetLightMap(Texture2D texture)
        {
            lightMap.SetValue(texture);
        }
        public void SetBloomFilter(Texture2D texture)
        {
            bloomFilter.SetValue(texture);
        }
        public void SetBlurH(Texture2D texture)
        {
            blurH.SetValue(texture);
        }
        public void SetBlurV(Texture2D texture)
        {
            blurV.SetValue(texture);
        }
        public void SetScreenSize(Vector2 screenSize)
        {
            this.screenSize.SetValue(screenSize);
        }
        public void SetRadius(float radius)
        {
            this.radius.SetValue(radius);
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
            lightPosition?.SetValue(pos);
        }
        public void SetCameraPosition(Vector3 pos)
        {
            cameraPosition?.SetValue(pos);
        }
        public void SetLightAmbientColor(Vector3 color)
        {
            lightAmbientColor?.SetValue(color); 
        }
        public void SetLightDiffuseColor(Vector3 color)
        {
            lightDiffuseColor?.SetValue(color);
        }
        public void SetLightSpecularColor(Vector3 color)
        { 
            lightSpecularColor?.SetValue(color);
        }
        public void SetKA(float value)
        {
            KA?.SetValue(value);
        }
        public void SetKD(float value)
        {
            KD?.SetValue(value);
        }
        public void SetKS(float value)
        {
            KS?.SetValue(value);
        }
        public void SetShininess(float value)
        {
            shininess?.SetValue(value);
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
