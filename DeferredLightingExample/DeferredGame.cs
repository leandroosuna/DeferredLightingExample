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
        
        int maxPointLights = 100;

        public DeferredGame()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            screenWidth = 1280;
            screenHeight = 720;

            Graphics.GraphicsProfile = GraphicsProfile.HiDef; //Enables shader model 5.0, DX 11, MRT, AlphaBlendDisable, etc.
            Graphics.IsFullScreen = false;
            Window.IsBorderless = true;
            Graphics.PreferredBackBufferWidth = screenWidth;
            Graphics.PreferredBackBufferHeight = screenHeight;
            Window.Position = new Point(320, 180);
            
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
            lightsManager.ambientLight = new AmbientLight(new Vector3(20, 50, 20), Vector3.One, Vector3.One, Vector3.One);

            // Create many point lights
            generatePointLights();

            // Create the render targets we are going to use
            setupRenderTargets();
           
        }

        

        float deltaTimeU;
        
        List<Keys> heldDown = new List<Keys>();
        protected override void Update(GameTime gameTime)
        {
            deltaTimeU = (float)gameTime.ElapsedGameTime.TotalSeconds;

            
            handleInput(deltaTimeU);

            // Update camera input and values
            camera.Update(deltaTimeU);
            // Updater for many lights
            updatePointLights(deltaTimeU);
            
            
            lightsManager.Update(deltaTimeU);

            base.Update(gameTime);
        }
        double time = 0f;
        double frameTime;
        int fps;
        float deltaTimeD;
        bool debugRTs = false;
        protected override void Draw(GameTime gameTime)
        {
            deltaTimeD = (float)gameTime.ElapsedGameTime.TotalSeconds;
            time += deltaTimeD;
            time %= 0.12;
            if (time <= .025)
            {
                fps = (int)(1 / deltaTimeD);
                frameTime = deltaTimeD * 1000;
            }

            basicModelEffect.SetView(camera.view);
            basicModelEffect.SetProjection(camera.projection);
            deferredEffect.SetView(camera.view);
            deferredEffect.SetProjection(camera.projection);
            deferredEffect.SetCameraPosition(camera.position);

            /// Using Multiple Render Targets for efficiency (G-Buffers)
            /// Target 1 (colorTarget) RGB = color, A = KD
            /// Target 2 (normalTarget) RGB = normal(scaled), A = KS
            /// Target 3 (positionTarget) RGB = world position, A = shininess(scale if necessary)
            /// Target 4 (not in use, but could be used) 
            ///
            /// Anything that you draw in this step must write to all of the textures
            /// BlendState should be NonPremultiplied or Opaque, transparent objects are not supported.
            /// the shader technique used must have "AlphaBlendEnable = FALSE;"
            /// GraphicsProfile.HiDef should be used, as well as shader model 5.0
            /// 
            GraphicsDevice.SetRenderTargets(colorTarget, normalTarget, positionTarget, bloomFilterTarget);
            GraphicsDevice.BlendState = BlendState.NonPremultiplied;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            
            // Draw a simple plane, enable lighting on it
            drawPlane();
            // Draw the geometry of the lights in the scene, so that we can see where the generators are
            lightsManager.DrawLightGeo();

            /// Now we calculate the lights. first we start by sending the targets from before as textures
            /// First, we use a fullscreen quad to calculate the ambient light, as a baseline (optional)
            /// Then, we iterate our point lights and render them as spheres in the correct position. 
            /// This will launch pixel shader functions only for the necessary pixels in range of that light.
            /// From the G-Buffer we sample the required information for that pixel, and we compute the color
            /// BlendState should be additive, to correctly sum up the contributions of multiple lights in
            /// the same pixel.
            /// For pixels that shouldnt be lit, for example the light geometry, normals are set to rgb = 0
            /// and we can use that to simply output white in our lightTarget for that pixel.
            GraphicsDevice.SetRenderTargets(lightTarget, blurHTarget, blurVTarget) ;
            GraphicsDevice.BlendState = BlendState.Additive;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            deferredEffect.SetColorMap(colorTarget);
            deferredEffect.SetNormalMap(normalTarget);
            deferredEffect.SetPositionMap(positionTarget);
            deferredEffect.SetBloomFilter(bloomFilterTarget);
            lightsManager.Draw();


            /// Finally, we have our color texture we calculated in step one, and the lights from step two
            /// we combine them here by simply multiplying them, finalColor = color * light, 
            /// using a final fullscreen quad pass.
            /// 
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
            string fpsStr = "FPS " + fps + " FT " + ft + " Lights " + lightCount;
            string str = "8: FullScreen Toggle, 9: Vsync Toggle, 0: ShowRTS";
            
            SpriteBatch.Begin();
            SpriteBatch.DrawString(Font, fpsStr, Vector2.Zero, Color.White);
            SpriteBatch.DrawString(Font, str, new Vector2(screenWidth - Font.MeasureString(str).X,0), Color.White);
            SpriteBatch.End();

            // TODO: Add your drawing code here

            base.Draw(gameTime);
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

                if (screenHeight == 720)
                {
                    screenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                    screenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                }
                else
                {
                    screenWidth = 1280;
                    screenHeight = 720;
                }
                Graphics.PreferredBackBufferWidth = screenWidth;
                Graphics.PreferredBackBufferHeight = screenHeight;
                Graphics.ApplyChanges();
                setupRenderTargets();
                
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
        void updatePointLights(float deltaTime)
        {
            timeL += deltaTime;
            for (int i = 0; i < maxPointLights; i++)
            {
                lights[i].position = new Vector3(MathF.Sin(timeL + offsetT[i]) * offsetR[i], 4, MathF.Cos(timeL + offsetT[i]) * offsetR[i]);
            }
        }
        
        List<int> offsetR = new List<int>();
        List<float> offsetT = new List<float>();
        List<LightVolume> lights = new List<LightVolume>();
        void generatePointLights()
        {
            var random = new Random();  
            for(int i = 0; i < maxPointLights; i++)
            {
                offsetR.Add((int)random.NextInt64(-300, 300));
                offsetT.Add((float) random.NextDouble() * 50);

                var randomColor = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                while(randomColor == Vector3.Zero || randomColor ==  Vector3.One)
                {
                    randomColor = new Vector3(random.NextInt64(0, 2), random.NextInt64(0, 2), random.NextInt64(0, 2));
                }
                var light = new PointLight(Vector3.Zero, 15f, randomColor, randomColor);
                lightsManager.register(light);
                lights.Add(light);
                light.hasLightGeo = true;
            }

        }
        
    }
}