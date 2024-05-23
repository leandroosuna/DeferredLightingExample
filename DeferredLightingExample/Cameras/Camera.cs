using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeferredLightingExample.Cameras
{
    public class Camera
    {
        public Vector3 position;
        public Vector3 frontDirection;
        public Vector3 rightDirection;
        public Vector3 upDirection;

        public Matrix view, projection;
        public Matrix viewProjection;

        public float fieldOfView;
        public float aspectRatio;
        public float nearPlaneDistance;
        public float farPlaneDistance;
        

        public float yaw;
        public float pitch;
        bool mouseLocked = true;
        Vector2 mouseDelta;
        float mouseSensitivity = .5f;
        float mouseSensAdapt = .06f;
        System.Drawing.Point center;

        public float moveSpeed = 5f;
        
        public BoundingFrustum frustum;

        DeferredGame game;
        public Camera(float aspectRatio, Point screenCenter)
        {
            game = DeferredGame.GetInstance();
            frustum = new BoundingFrustum(Matrix.Identity);
            fieldOfView = MathHelper.ToRadians(100);
            this.aspectRatio = aspectRatio;
            position = new Vector3(-2, 6, 2);
            nearPlaneDistance = 1; 
            farPlaneDistance = 1000;
            yaw = 310;
            pitch = 0;
            center = new System.Drawing.Point(screenCenter.X, screenCenter.Y);
            
            UpdateCameraVectors();
            CalculateView();
            CalculateProjection();
        }
        
        public void Update(float deltaTime)
        {
            updateMousePositionDelta();
            updateKeys(deltaTime);
            yaw += mouseDelta.X;
            if (yaw < 0)
                yaw += 360;
            yaw %= 360;

            pitch -= mouseDelta.Y;

            if (pitch > 89.0f)
                pitch = 89.0f;
            else if (pitch < -89.0f)
                pitch = -89.0f;

            UpdateCameraVectors();
            CalculateView();

            frustum.Matrix = view * projection;
        }
        Vector2 mousePosition;
        Vector2 delta;
        public void updateMousePositionDelta()
        {
            mousePosition.X = System.Windows.Forms.Cursor.Position.X;
            mousePosition.Y = System.Windows.Forms.Cursor.Position.Y;

            delta.X = mousePosition.X - center.X;
            delta.Y = mousePosition.Y - center.Y;

            mouseDelta = delta * mouseSensitivity * mouseSensAdapt;
            if (mouseLocked)
                System.Windows.Forms.Cursor.Position = center;
        }
        void updateKeys(float deltaTime)
        {
            var frontNoY = new Vector3(frontDirection.X, 0, frontDirection.Z);
            var rightNoY = new Vector3(rightDirection.X, 0, rightDirection.Z);

            var dirChanged = false;
            var dir = Vector3.Zero;
            var keyState = Keyboard.GetState();
            
            var speed = moveSpeed;
            if (keyState.IsKeyDown(Keys.LeftShift))
                speed *= 3f;

            if (keyState.IsKeyDown(Keys.W))
            {
                dir += frontNoY;
                dirChanged = true;
            }
            if (keyState.IsKeyDown(Keys.A))
            {
                dir -= rightNoY;
                dirChanged = true;
            }
            if (keyState.IsKeyDown(Keys.S))
            {
                dir -= frontNoY;
                dirChanged = true;
            }
            if (keyState.IsKeyDown(Keys.D))
            {
                dir += rightNoY;
                dirChanged = true;
            }
            if (keyState.IsKeyDown(Keys.Space))
            {
                dir += Vector3.Up;
                dirChanged = true;
            }
            if (keyState.IsKeyDown(Keys.LeftControl))
            {
                dir += Vector3.Down;
                dirChanged = true;
            }

            if (dirChanged)
            {
                if (Vector3.Distance(dir, Vector3.Zero) > 0)
                {
                    dir = Vector3.Normalize(dir);

                    position += dir * speed * deltaTime;
                }
            }
        }
        void UpdateCameraVectors()
        {
            Vector3 tempFront;

            tempFront.X = MathF.Cos(MathHelper.ToRadians(yaw)) * MathF.Cos(MathHelper.ToRadians(pitch));
            tempFront.Y = MathF.Sin(MathHelper.ToRadians(pitch));
            tempFront.Z = MathF.Sin(MathHelper.ToRadians(yaw)) * MathF.Cos(MathHelper.ToRadians(pitch));

            frontDirection = Vector3.Normalize(tempFront);

            rightDirection = Vector3.Normalize(Vector3.Cross(frontDirection, Vector3.Up));
            upDirection = Vector3.Normalize(Vector3.Cross(rightDirection, frontDirection));
        }
        void CalculateView()
        {
            view = Matrix.CreateLookAt(position, position + frontDirection, upDirection);
        }
        void CalculateProjection()
        {
            projection = Matrix.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance);
        }
        public bool frustumContains(BoundingSphere collider)
        {
            return !frustum.Contains(collider).Equals(ContainmentType.Disjoint);
        }
        public bool frustumContains(BoundingBox collider)
        {
            return !frustum.Contains(collider).Equals(ContainmentType.Disjoint);
        }
        public bool frustumContains(Vector3 point)
        {
            return !frustum.Contains(point).Equals(ContainmentType.Disjoint);
        }

        public void ResetToCenter()
        {
            yaw = 310;
            pitch = -36;
            UpdateCameraVectors();
            CalculateView();

        }

    }
}
