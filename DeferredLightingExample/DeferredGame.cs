using DeferredLightingExample.Cameras;
using DeferredLightingExample.Effects;
using DeferredLightingExample.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace DeferredLightingExample
{
    public class DeferredGame : Game
    {
        private GraphicsDeviceManager Graphics;
        private SpriteBatch SpriteBatch;
        SpriteFont Font;

        public const string ContentFolderEffects = "Effects/";
        public const string ContentFolder3D = "Models/";
        public const string ContentFolderFonts = "Fonts/";

        public Point screenCenter;
        public DeferredEffect deferredEffect;
        public BasicModelEffect basicModelEffect;
        public int screenWidth;
        public int screenHeight;

        Model sphere, cube, plane, lightSphere, lightCone;
        Texture2D planeTex;

        public Camera camera;
        public FullScreenQuad fullScreenQuad;

        public LightsManager lightsManager;

        RenderTarget2D colorTarget;
        RenderTarget2D normalTarget;
        RenderTarget2D positionTarget;
        RenderTarget2D bloomFilterTarget;
        RenderTarget2D blurHTarget;
        RenderTarget2D blurVTarget;
        RenderTarget2D lightTarget;

        static DeferredGame instance;
        
        int maxLights = 600;

        public DeferredGame()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            screenWidth = 1600;
            screenHeight = 900;

            Graphics.GraphicsProfile = GraphicsProfile.HiDef; //Enables shader model 5.0, DX 11, MRT, AlphaBlendDisable, etc.
            Graphics.IsFullScreen = false;
            Window.IsBorderless = true;
            Graphics.PreferredBackBufferWidth = screenWidth;
            Graphics.PreferredBackBufferHeight = screenHeight;
            Window.Position = new Point(0, 0);
            
            IsFixedTimeStep = false;
            Graphics.SynchronizeWithVerticalRetrace = true;
            Graphics.ApplyChanges();

            IsMouseVisible = false;
        }

        protected override void Initialize()
        {
            instance = this;

            var viewport = GraphicsDevice.Viewport;
            screenCenter = new Point(viewport.Width / 2, viewport.Height / 2);
            camera = new Camera(viewport.AspectRatio, screenCenter);

            fullScreenQuad = new FullScreenQuad(GraphicsDevice);
            SpriteBatch = new SpriteBatch(GraphicsDevice);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);

            Font = Content.Load<SpriteFont>(ContentFolderFonts + "tahoma/15");
            
            // These managers allow faster effect parameter access
            basicModelEffect = new BasicModelEffect("basic");
            deferredEffect = new DeferredEffect("deferred");

            sphere = Content.Load<Model>(ContentFolder3D + "Basic/sphere");
            cube = Content.Load<Model>(ContentFolder3D + "Basic/cube");
            plane = Content.Load<Model>(ContentFolder3D + "Basic/plane");
            lightSphere = Content.Load<Model>(ContentFolder3D + "Basic/lightSphere");
            lightCone = Content.Load<Model>(ContentFolder3D + "Basic/cone");

            planeTex = Content.Load<Texture2D>(ContentFolder3D + "Basic/tex/planeTex");
            // Assign the correct effect for each model
            AssignEffect(sphere, basicModelEffect.effect);
            AssignEffect(cube, basicModelEffect.effect);
            AssignEffect(plane, basicModelEffect.effect);

            AssignEffect(lightSphere, deferredEffect.effect);
            AssignEffect(lightCone, deferredEffect.effect);

            // Lights are child classes of LightVolume, lights are rendered as 3D objects
            
            LightVolume.Init(sphere, lightSphere, lightCone, cube);

            // This manager handles the lightVolumes, updating and drawing them
            lightsManager = new LightsManager();
            lightsManager.ambientLight = new AmbientLight(new Vector3(20, 50, 20), new Vector3(1,.7f,1), Vector3.One, Vector3.One);

            // Create many lights
            generateLights();

            // Create the render targets we are going to use
            setupRenderTargets();
           
        }

        

        float deltaTimeU;
        
        List<Keys> heldDown = new List<Keys>();
        protected override void Update(GameTime gameTime)
        {
            deltaTimeU = (float)gameTime.ElapsedGameTime.TotalSeconds;


            handleInput(deltaTimeU);

            if (!demo)
            {
                // Update camera input and values
                camera.Update(deltaTimeU);
                // Updater for many lights
                updateLights(deltaTimeU);
            
            
                lightsManager.Update(deltaTimeU);
            }

            base.Update(gameTime);
        }
        double time = 0f;
        double frameTime;
        int fps;
        float deltaTimeD;
        bool debugRTs = false;

        bool demo = false;
        int demoStep = 0;
        bool demoAuto = false;
        double demoTimer = 0;
        double lightTimer = 0;
        protected override void Draw(GameTime gameTime)
        {
            deltaTimeD = (float)gameTime.ElapsedGameTime.TotalSeconds;
            time += deltaTimeD;
            time %= 0.12;
            demoTimer += deltaTimeD;
            lightTimer += deltaTimeD;

            if (time <= .025)
            {
                fps = (int)(1 / deltaTimeD);
                frameTime = deltaTimeD * 1000;
            }
            if(demoAuto)
            {
                if(demoTimer >= 5)
                {
                    demoTimer = 0;
                
                
                    if(demoStep < 3)
                    {
                        demoStep++;
                        lightsManager.partialLightsCount = 0;
                    }
                    else
                    {
                        demoStep = 0;
                        demo = false;
                    }    
                }
            }
            
            basicModelEffect.SetView(camera.view);
            basicModelEffect.SetProjection(camera.projection);
            deferredEffect.SetView(camera.view);
            deferredEffect.SetProjection(camera.projection);
            deferredEffect.SetCameraPosition(camera.position);
            if(demo)
            {
                DrawDemo();
                return;
            }
            /// Using Multiple Render Targets for efficiency (G-Buffers)
            /// Target 1 (colorTarget) RGB = color, A = KD
            /// Target 2 (normalTarget) RGB = normal(scaled), A = KS
            /// Target 3 (positionTarget) RGB = world position, A = shininess(scale if necessary)
            /// Target 4 (bloomFilterTarget) RGB = filter, A = (not in use) 
            ///
            /// Filter: anything light emissive 
            /// 
            /// Anything that you draw in this step must write to all of the textures
            /// BlendState should be NonPremultiplied or Opaque, transparent objects are not supported.
            /// the shader technique used must have "AlphaBlendEnable = FALSE;"
            /// GraphicsProfile.HiDef should be used, as well as shader model 5.0
            DrawPass(0);
            
            /// Now we calculate the lights. first we start by sending the targets from before as textures
            /// First, we use a fullscreen quad to calculate the ambient light, as a baseline (optional)
            /// Then, we iterate our point lights and render them as spheres in the correct position. 
            /// This will launch pixel shader functions only for the necessary pixels in range of that light.
            /// From the G-Buffer we sample the required information for that pixel, and we compute the color
            /// BlendState should be additive, to correctly sum up the contributions of multiple lights in
            /// the same pixel.
            /// For pixels that shouldnt be lit, for example the light geometry, normals are set to rgb = 0
            /// and we can use that to simply output white in our lightTarget for that pixel.
            /// 
            /// We take advantage of the fullscreen quad rendered for the ambient light to blur the emissive tex
            /// horizontally and vertically to different textures at the same time
            DrawPass(1);

            /// Finally, we have our color texture we calculated in step one, the lights and blurred filter 
            /// from step two. We combine them here by simply multiplying them 
            /// finalColor = color * light + bloom
            /// using a final fullscreen quad pass 
            DrawPass(2);


            base.Draw(gameTime);
        }

        
        void DrawDemo()
        {
            var rec = new Rectangle(0, 0, screenWidth, screenHeight);
            var colorStr = "Target Color";
            var normalStr = "Target Normal";
            var positionStr = "Target Position";
            var bloomStr = "Target Bloom";
            var lightStr = "Target Light";
            var blurHStr = "Target BlurH";
            var blurVStr = "Target BlurV";
            var offset = 7;
            var offsetTL = new Vector2(offset, offset);
            var offsetTR = new Vector2(-offset, offset);
            var offsetTC = new Vector2(0, offset);
            var offsetBL = new Vector2(offset, -offset);
            var offsetBR = new Vector2(-offset, -offset);

            var str = "Dibujando ";
            switch (demoStep)
            {
                case 0:
                    GraphicsDevice.SetRenderTargets(colorTarget, normalTarget, positionTarget, bloomFilterTarget);
                    GraphicsDevice.BlendState = BlendState.NonPremultiplied;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                    drawPlane();

                    GraphicsDevice.SetRenderTargets(null);
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    SpriteBatch.Begin(blendState: BlendState.Opaque);

                    SpriteBatch.Draw(colorTarget, Vector2.Zero, rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(normalTarget, new Vector2(0, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(positionTarget, new Vector2(screenWidth - screenWidth / 2, 0), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(bloomFilterTarget, new Vector2(screenWidth - screenWidth / 2, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);

                    SpriteBatch.End();

                    SpriteBatch.Begin();
                    SpriteBatch.DrawString(Font, colorStr, Vector2.Zero + offsetTL, Color.White);
                    SpriteBatch.DrawString(Font, normalStr, new Vector2(0, screenHeight - Font.MeasureString(normalStr).Y) + offsetBL, Color.White);
                    SpriteBatch.DrawString(Font, positionStr, new Vector2(screenWidth - Font.MeasureString(positionStr).X, 0) + offsetTR, Color.White);
                    SpriteBatch.DrawString(Font, bloomStr, new Vector2(screenWidth - Font.MeasureString(bloomStr).X, screenHeight - Font.MeasureString(bloomStr).Y) + offsetBR, Color.White);
                    str += "plano";
                    SpriteBatch.DrawString(Font, str, new Vector2(screenWidth/2 - Font.MeasureString(str).X/2, screenHeight / 2 + Font.MeasureString(str).Y / 2), Color.White);

                    SpriteBatch.End();


                    break;
                case 1:
                    GraphicsDevice.SetRenderTargets(colorTarget, normalTarget, positionTarget, bloomFilterTarget);
                    GraphicsDevice.BlendState = BlendState.NonPremultiplied;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                    drawPlane();

                    if (lightTimer >= .1)
                    {
                        lightTimer = 0;
                        lightsManager.partialLightsCount++;
                        lightsManager.partialLightsCount%= lightsManager.lightsToDraw.Count;
                    }
                    lightsManager.DrawLightGeoPartial();
                    
                    GraphicsDevice.SetRenderTargets(null);
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    SpriteBatch.Begin(blendState: BlendState.Opaque);
                    
                    SpriteBatch.Draw(colorTarget, Vector2.Zero, rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(normalTarget, new Vector2(0, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(positionTarget, new Vector2(screenWidth - screenWidth / 2, 0), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(bloomFilterTarget, new Vector2(screenWidth - screenWidth / 2, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);

                    SpriteBatch.End();

                    
                    SpriteBatch.Begin();
                    SpriteBatch.DrawString(Font, colorStr, Vector2.Zero + offsetTL, Color.White);
                    SpriteBatch.DrawString(Font, normalStr, new Vector2(0, screenHeight - Font.MeasureString(normalStr).Y) + offsetBL, Color.White);
                    SpriteBatch.DrawString(Font, positionStr, new Vector2(screenWidth - Font.MeasureString(positionStr).X, 0) + offsetTR, Color.White);
                    SpriteBatch.DrawString(Font, bloomStr, new Vector2(screenWidth - Font.MeasureString(bloomStr).X, screenHeight - Font.MeasureString(bloomStr).Y) + offsetBR, Color.White);
                    str += "geometria luz "+lightsManager.partialLightsCount;
                    SpriteBatch.DrawString(Font, str, new Vector2(screenWidth / 2 - Font.MeasureString(str).X / 2, screenHeight / 2 + Font.MeasureString(str).Y / 2), Color.White);

                    SpriteBatch.End();


                    break;
                case 2:
                    GraphicsDevice.SetRenderTargets(lightTarget, blurHTarget, blurVTarget);
                    GraphicsDevice.BlendState = BlendState.Additive;
                    GraphicsDevice.DepthStencilState = DepthStencilState.None;

                    deferredEffect.SetColorMap(colorTarget);
                    deferredEffect.SetNormalMap(normalTarget);
                    deferredEffect.SetPositionMap(positionTarget);
                    deferredEffect.SetBloomFilter(bloomFilterTarget);
                    lightsManager.DrawAmbient();

                    GraphicsDevice.SetRenderTargets(null);
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    SpriteBatch.Begin(blendState: BlendState.Opaque);

                    SpriteBatch.Draw(lightTarget, new Vector2(screenWidth / 4, 0), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(blurHTarget, new Vector2(0, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(blurVTarget, new Vector2(screenWidth - screenWidth / 2, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);

                    SpriteBatch.End();

                    SpriteBatch.Begin();
                    SpriteBatch.DrawString(Font, lightStr, new Vector2(screenWidth/2 - Font.MeasureString(lightStr).X/2, 0) + offsetTC, Color.White);
                    SpriteBatch.DrawString(Font, blurHStr, new Vector2(0, screenHeight - Font.MeasureString(normalStr).Y) + offsetBL, Color.White);
                    SpriteBatch.DrawString(Font, blurVStr, new Vector2(screenWidth - Font.MeasureString(bloomStr).X, screenHeight - Font.MeasureString(bloomStr).Y) + offsetBR, Color.White);
                    
                    str += "luz ambiente, blur horizontal, blur vertical";
                    SpriteBatch.DrawString(Font, str, new Vector2(screenWidth / 2 - Font.MeasureString(str).X / 2, screenHeight / 2 + Font.MeasureString(str).Y / 2), Color.White);

                    SpriteBatch.End();

                    break;
                case 3:
                    GraphicsDevice.SetRenderTargets(lightTarget, blurHTarget, blurVTarget);
                    GraphicsDevice.BlendState = BlendState.Additive;
                    GraphicsDevice.DepthStencilState = DepthStencilState.None;

                    deferredEffect.SetColorMap(colorTarget);
                    deferredEffect.SetNormalMap(normalTarget);
                    deferredEffect.SetPositionMap(positionTarget);
                    deferredEffect.SetBloomFilter(bloomFilterTarget);
                    lightsManager.DrawAmbient();
                    if (lightTimer >= .1)
                    {
                        lightTimer = 0;
                        lightsManager.partialLightsCount++;
                        lightsManager.partialLightsCount %= lightsManager.lightsToDraw.Count;
                    }
                    lightsManager.DrawLightPartial();

                    GraphicsDevice.SetRenderTargets(null);
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    SpriteBatch.Begin(blendState: BlendState.Opaque);

                    SpriteBatch.Draw(lightTarget, new Vector2(screenWidth / 4, 0), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(blurHTarget, new Vector2(0, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);
                    SpriteBatch.Draw(blurVTarget, new Vector2(screenWidth - screenWidth / 2, screenHeight - screenHeight / 2), rec, Color.White, 0f, Vector2.Zero, .5f, SpriteEffects.None, 0f);

                    SpriteBatch.End();

                    SpriteBatch.Begin();
                    SpriteBatch.DrawString(Font, lightStr, new Vector2(screenWidth / 2 - Font.MeasureString(lightStr).X / 2, 0) + offsetTC, Color.White);
                    SpriteBatch.DrawString(Font, blurHStr, new Vector2(0, screenHeight - Font.MeasureString(normalStr).Y) + offsetBL, Color.White);
                    SpriteBatch.DrawString(Font, blurVStr, new Vector2(screenWidth - Font.MeasureString(bloomStr).X, screenHeight - Font.MeasureString(bloomStr).Y) + offsetBR, Color.White);

                    str += "luz "+lightsManager.partialLightsCount;
                    SpriteBatch.DrawString(Font, str, new Vector2(screenWidth / 2 - Font.MeasureString(str).X / 2, screenHeight / 2 + Font.MeasureString(str).Y / 2), Color.White);

                    SpriteBatch.End();

                    break;
                
            }
        }

        void DrawPass(int pass)
        {
            switch(pass)
            {
                case 0:
                    GraphicsDevice.SetRenderTargets(colorTarget, normalTarget, positionTarget, bloomFilterTarget);
                    GraphicsDevice.BlendState = BlendState.NonPremultiplied;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    // Draw a simple plane, enable lighting on it
                    drawPlane();
                    // Draw the geometry of the lights in the scene, so that we can see where the generators are
                    lightsManager.DrawLightGeo();
                    break;
                case 1:
                    GraphicsDevice.SetRenderTargets(lightTarget, blurHTarget, blurVTarget);
                    GraphicsDevice.BlendState = BlendState.Additive;
                    GraphicsDevice.DepthStencilState = DepthStencilState.None;

                    deferredEffect.SetColorMap(colorTarget);
                    deferredEffect.SetNormalMap(normalTarget);
                    deferredEffect.SetPositionMap(positionTarget);
                    deferredEffect.SetBloomFilter(bloomFilterTarget);
                    lightsManager.Draw();
                    break;
                case 2:
                    GraphicsDevice.SetRenderTarget(null);
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                    deferredEffect.SetLightMap(lightTarget);
                    deferredEffect.SetScreenSize(new Vector2(screenWidth, screenHeight));
                    deferredEffect.SetTech("integrate");

                    deferredEffect.SetBlurH(blurHTarget);
                    deferredEffect.SetBlurV(blurVTarget);

                    fullScreenQuad.Draw(deferredEffect.effect);

                    var lightCount = lightsManager.lightsToDraw.Count;
                    var rec = new Rectangle(0, 0, screenWidth, screenHeight);

                    /// In this example, by hitting key 0 you can see the targets in the corners of the screen
                    if (debugRTs)
                    {
                        SpriteBatch.Begin(blendState: BlendState.Opaque);

                        SpriteBatch.Draw(colorTarget, Vector2.Zero, rec, Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
                        SpriteBatch.Draw(normalTarget, new Vector2(0, screenHeight - screenHeight / 4), rec, Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
                        SpriteBatch.Draw(positionTarget, new Vector2(screenWidth - screenWidth / 4, 0), rec, Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
                        SpriteBatch.Draw(lightTarget, new Vector2(screenWidth - screenWidth / 4, screenHeight - screenHeight / 4), rec, Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);

                        SpriteBatch.End();
                    }

                    string ft = (frameTime * 1000).ToString("0,####");
                    string fpsStr = "FPS " + fps + " FT " + ft + " Lights " + lightCount + "/" + lights.Count;
                    string str = "9: Vsync Toggle, 0: ShowRTS";

                    SpriteBatch.Begin();
                    SpriteBatch.DrawString(Font, fpsStr, Vector2.Zero, Color.White);
                    SpriteBatch.DrawString(Font, str, new Vector2(screenWidth - Font.MeasureString(str).X, 0), Color.White);
                    SpriteBatch.End();
                    break;
            }
        }
        void handleInput(float deltaTime)
        {
            var keyState = Keyboard.GetState();
            heldDown.RemoveAll(key => keyState.IsKeyUp(key));

            if (keyState.IsKeyDown(Keys.Escape))
                Exit();
            if (keyState.IsKeyDown(Keys.D0) && !heldDown.Contains(Keys.D0))
            {
                heldDown.Add(Keys.D0);

                debugRTs = !debugRTs;
            }
            if (keyState.IsKeyDown(Keys.D9) && !heldDown.Contains(Keys.D9))
            {
                heldDown.Add(Keys.D9);

                Graphics.SynchronizeWithVerticalRetrace = !Graphics.SynchronizeWithVerticalRetrace;
                Graphics.ApplyChanges();
            }
            if (keyState.IsKeyDown(Keys.D8) && !heldDown.Contains(Keys.D8))
            {
                heldDown.Add(Keys.D8);
                maxLights += 200;
                generateLights();
                
            }

            if (keyState.IsKeyDown(Keys.D7) && !heldDown.Contains(Keys.D7))
            {
                heldDown.Add(Keys.D7);
                if(maxLights >=400)
                    maxLights -= 200;
                
                generateLights();

            }
            if (keyState.IsKeyDown(Keys.D1) && !heldDown.Contains(Keys.D1))
            {
                heldDown.Add(Keys.D1);
                camera.mouseLocked = !camera.mouseLocked;
                if(camera.mouseLocked)
                {
                    System.Windows.Forms.Cursor.Position = camera.center;
                }
                else
                {
                    camera.mouseDelta = Vector2.Zero;
                }

            }
            if (keyState.IsKeyDown(Keys.N) && !heldDown.Contains(Keys.N))
            {
                heldDown.Add(Keys.N);
                demo = true;
                demoAuto = true;
                
            }
            if (keyState.IsKeyDown(Keys.M) && !heldDown.Contains(Keys.M))
            {
                heldDown.Add(Keys.M);
                demo = !demo;
                demoStep = 0;
                demoAuto = false;
            }
            if (keyState.IsKeyDown(Keys.K) && !heldDown.Contains(Keys.K))
            {
                heldDown.Add(Keys.K);
                if(demoStep>0)
                    demoStep--;
                lightsManager.partialLightsCount = 0;
            }
            if (keyState.IsKeyDown(Keys.L) && !heldDown.Contains(Keys.L))
            {
                heldDown.Add(Keys.L);
                if (demoStep < 3)
                    demoStep++;
                lightsManager.partialLightsCount = 0;
            }

        }
        private void drawPlane()
        {
            basicModelEffect.SetTech("colorTex_lightEn");
            basicModelEffect.SetColorTexture(planeTex);
            basicModelEffect.SetTiling(Vector2.One * 500);

            foreach (var mesh in plane.Meshes)
            {
                var w = mesh.ParentBone.Transform * Matrix.CreateScale(10f) * Matrix.CreateTranslation(0, 0, 0);
                basicModelEffect.SetWorld(w);
                basicModelEffect.SetKA(0.3f);
                basicModelEffect.SetKD(0.8f);
                basicModelEffect.SetKS(0.8f);
                basicModelEffect.SetShininess(30f);

                basicModelEffect.SetInverseTransposeWorld(Matrix.Invert(Matrix.Transpose(w)));

                mesh.Draw();
            }
            basicModelEffect.SetTiling(Vector2.One);
        }

        public static void AssignEffect(Model m, Effect e)
        {
            foreach (var mesh in m.Meshes)
                foreach (var meshPart in mesh.MeshParts)
                    meshPart.Effect = e;
        }
        public static DeferredGame GetInstance()
        {
            return instance;
        }
        void setupRenderTargets()
        {
            colorTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            normalTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            positionTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            bloomFilterTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            blurHTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            blurVTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            lightTarget = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
        }
        float timeL = 0f;
        void updateLights(float deltaTime)
        {
            timeL += deltaTime  * .5f;
            for (int i = 0; i < maxLights; i++)
            {
                var x = offsetX[i] + MathF.Sin(timeL + offsetT[i]) * offsetR[i];
                var z = offsetZ[i] + MathF.Cos(timeL + offsetT[i]) * offsetR[i];
                if (i%2 == 0)
                    lights[i].position = new Vector3(x, 4 + offsetY[i], z);
                else
                    lights[i].position = new Vector3(z, 4 + offsetY[i], x);
            }
        }
        
        List<int> offsetR = new List<int>();
        List<float> offsetT = new List<float>();
        List<float> offsetX = new List<float>();
        List<float> offsetZ = new List<float>();
        List<float> offsetY = new List<float>();

        List<LightVolume> lights = new List<LightVolume>();
        void generateLights()
        {
            foreach(var l in lights)
            {
                lightsManager.destroy(l);
            }
            lights.Clear();
            var random = new Random();  
            for(int i = 0; i < maxLights; i++)
            {
                offsetR.Add((int)random.NextInt64(-50, 50));
                offsetT.Add((float) random.NextDouble() * 50);
                offsetX.Add((float)random.NextDouble() * 1000);
                offsetZ.Add((float)random.NextDouble() * 1000);

                var randomColor = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                while(randomColor == Vector3.Zero || randomColor ==  Vector3.One)
                {
                    randomColor = new Vector3(random.NextInt64(0, 2), random.NextInt64(0, 2), random.NextInt64(0, 2));
                }
                LightVolume light;

                //fix orientation of conelights
                //if (i % 3 == 0)
                //{
                //    var yaw = (float)random.NextDouble() * MathHelper.TwoPi;
                //    light = new ConeLight(Vector3.Zero, 15f, yaw, 0f, MathHelper.PiOver2, randomColor, randomColor);
                //    offsetY.Add(-3.5f);
                //}
                //else
                //{
                    light = new PointLight(Vector3.Zero, 15f, randomColor, randomColor);
                    offsetY.Add(0f);
                //}
                lightsManager.register(light);
                lights.Add(light);
                light.hasLightGeo = true;
            }

        }
        
    }
}