using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationAuthoring
{
        
    [System.Serializable]
    public class Point
    {
        [SerializeField] private Matrix4x4 Transformation = Matrix4x4.identity;
        [SerializeField] private Vector3 Position;
        [SerializeField] private Vector3 Direction;
        
        public Point()
        {
            this.Position = Vector3.zero;
            this.Direction = Vector3.forward;
        }
        public virtual Vector3 GetPosition()
        {
            return Position;
        }
        public virtual void SetPosition(Vector3 pos)
        {  
            Position = pos;
            SetTransformation(Matrix4x4.TRS(GetPosition(), Quaternion.LookRotation(GetDirection(), Vector3.up), Vector3.one));
        }
        public virtual void SetDirection(Vector3 dir)
        {    
            Direction = dir;
            SetTransformation(Matrix4x4.TRS(GetPosition(), Quaternion.LookRotation(GetDirection(), Vector3.up), Vector3.one));
        }
        public Vector3 GetDirection()
        {
            return Direction;
        }

        public Matrix4x4 GetTransformation(){
            return Transformation;
        }

        public void SetTransformation(Matrix4x4 matrix){
            Transformation = matrix;
        }
    }
}