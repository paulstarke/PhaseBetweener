using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PathCreation.Utility;

namespace AnimationAuthoring
{
    [System.Serializable]
    public class Path
    {
        public int FPS;
        public bool isLooping;
        public Point[] Points;
        [SerializeReference] public List<ControlPoint> ControlPoints;
        public float TimeInterval;
        public Authoring Authoring;
        
        public bool AutoDirections;
        public Path(Authoring authoring, bool isLooping = false, 
            int fps = 30, float timeInterval = 1f)
        {
            this.Authoring = authoring;
            this.FPS = fps;
            this.TimeInterval = timeInterval;
            this.isLooping = isLooping;
            this.AutoDirections = false;
            ControlPoints = new List<ControlPoint>();

            AddControlPoint(authoring.transform.position);
            AddControlPoint(authoring.transform.position + 4*Vector3.right + Vector3.down);
            AddControlPoint(authoring.transform.position + 4*Vector3.right + 4*Vector3.forward);

            // Set Direction
            for (int i = 0; i < NumControlPoints; i++)
            {
                int nextI = i + 1;
                if (nextI >= NumControlPoints)
                {
                    //Got to start
                    if (isLooping)
                    {
                        nextI %= NumControlPoints;
                    }
                    else
                    {
                        ControlPoints[i].SetDirection(ControlPoints[i].GetPosition() - ControlPoints[i-1].GetPosition());
                        break;
                    }
                }
                ControlPoints[i].SetDirection(ControlPoints[nextI].GetPosition() - ControlPoints[i].GetPosition());
            }
            GenerateAllPoints(TimeDelta);
        }

        public int NumPoints {
            get
            {
                return Points.Length;
            }
        }

        public float TimeDelta
        {
            get
            {
                return 1 / (float)FPS;
            }
            set
            {
                TimeDelta = value;
            }
        }

        public int NumControlPoints {
            get
            {
                return ControlPoints.Count;
            }
        }

        public int NumSegments{
            get
            {
                return isLooping ? ControlPoints.Count : ControlPoints.Count - 1;
            }
        }

        public int NumPointsInSegment{
            get
            {
                return (int) (FPS * TimeInterval);
            }
        }

        #region Working with current data
        public int GetClosestControlPointIndexInRange(Vector3 worlPoint, float dstToControlPoint){
            int closestControlPointIndex = -1;
            for (int i = 0; i < NumControlPoints; i++)
            {
                float dst = Vector3.Distance(worlPoint, ControlPoints[i].GetPosition());
                if (dst < dstToControlPoint)
                {
                    dstToControlPoint = dst;
                    closestControlPointIndex = i;
                }
            }
            return closestControlPointIndex;
        }
        public Vector3 GetClosestPositionOnPath(Vector3 worldPoint)
        {
            TimeOnPathData data = CalculateClosestTimeBetweenInterpolatedPoints(worldPoint);
            return Vector3.Lerp(Points[data.previousIndex].GetPosition(), Points[data.nextIndex].GetPosition(), data.percentBetweenIndices);
        }

        public float GetClosestTimeOnPath(Vector3 worldPoint)
        {
            TimeOnPathData data = CalculateClosestTimeBetweenInterpolatedPoints(worldPoint);
            float a = GetTimestampByPointIndex(data.previousIndex);
            float b = GetTimestampByPointIndex(data.nextIndex);
            if (Math.Abs(a - b) > TimeInterval) return 0f;
            return Mathf.Lerp(a, b, data.percentBetweenIndices);
        }

        public Point[] GetPointsInSegment(float timestamp)
        {
            int segmentIndex = GetClosestIndex(timestamp);
            Point[] p = new Point[NumPointsInSegment];
            for(int i=0; i<p.Length; i++){
                p[i] = GetPoint(segmentIndex*TimeInterval + TimeDelta * i);
            }
            return p;
        }

        public Point GetPoint(float timestamp)
        {
            return Points[GetPointIndex(timestamp)];
        }

        public int GetPointIndex(float timestamp)
        {
            int index = (int)Math.Round((Points.Length / GetTotalTime()) * FilterTimestamp(timestamp));
            return Mathf.Clamp(index, 0, Points.Length - 1);
        }
        #endregion

        #region Update / Compute Points
        public Point CreatePoint(float timestamp, bool directToNextPoint)
        {
            // Precompute Features
            Point point = new Point();
            point.SetPosition(CalculatePointPosition(timestamp));
            if(directToNextPoint){
                // direct to point in future depending on NumPointsInSegment
                point.SetDirection((CalculatePointPosition(timestamp + (NumPointsInSegment/5)*TimeDelta) - CalculatePointPosition(timestamp)).normalized);
            } else {
                point.SetDirection(CalculatePointDirection(timestamp).normalized);
            }
            
            return point;
        }

        public void GenerateAllPoints(float deltaTime)
        {
            var startTime = System.DateTime.Now;
            Points = new Point[CalculateNumberPoints()];

            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = CreatePoint(GetTimestampByPointIndex(i), AutoDirections);
            }

            var elapsed = (DateTime.Now - startTime).Milliseconds;
            //Debug.Log("Update whole path: " + elapsed + "ms");

            foreach(ControlPoint cp in ControlPoints) {
                cp.Callback();
            }

            Authoring.SortChildren();
        }

        public enum UPDATETYPE{
            ADD, INSERT, DELETE, NONE
        }
        public void UpdateAroundControlPoint(float timestamp, UPDATETYPE type)
        {
            //Faster
            if(NumControlPoints <= 4){
                GenerateAllPoints(TimeDelta);
                return;
            }

            //TODO: Has to be on same points during the update window...
            Point[] points = Points;
            if(type == UPDATETYPE.ADD){
                ArrayExtensions.Resize<Point>(ref points ,CalculateNumberPoints());
            }else if(type == UPDATETYPE.INSERT){
                points = new Point[CalculateNumberPoints()];
                int insertIndex = (int)Math.Round(GetControlPointTimestamp(timestamp, -1) / TimeDelta);
                for (int i=0; i<insertIndex; i++){
                    points[i] = Points[i];
                }
                for (int i=insertIndex+NumPointsInSegment; i<points.Length; i++){
                    points[i] = Points[i-NumPointsInSegment];
                }
            }

            Point[] tmp = new Point[points.Length];
            int start = (int)Math.Round(GetControlPointTimestamp(timestamp, -2) / TimeDelta);
            int end = (int)Math.Round(GetControlPointTimestamp(timestamp, 2) / TimeDelta);

            // Debug.Log("start: " + start + " end: " + end + " PointsNumber: " +tmp.Length);
            /// ___ == new calculation ; ... take existing points
            ///   End
            ///   O_________O
            ///  .           \
            /// O             O
            ///  .   Start   /
            ///    O________O
            if (end > start)
            {   
                int[] indicies = ArrayExtensions.GetRangeExclusive(0, Mathf.Clamp(start,0,start));
                Point[] a = ArrayExtensions.GatherByIndices<Point>(points, indicies);
                indicies = ArrayExtensions.GetRangeExclusive(Mathf.Clamp(end,end,tmp.Length), tmp.Length);
                Point[] b = ArrayExtensions.GatherByIndices<Point>(points, indicies);
                for (int i = start; i < end; i++)
                {
                    ArrayExtensions.Append<Point>(ref a, CreatePoint(i * TimeDelta, AutoDirections));
                }
                tmp = ArrayExtensions.Concat<Point>(a, b);
            }
            ///   Start
            ///   O ........ O
            ///  /            .
            /// O             O End
            ///  \           /
            ///    O________O
            else
            {
                for (int i = 0; i < end; i++)
                {
                    tmp[i] = CreatePoint(i * TimeDelta, AutoDirections);
                }
                for (int i = end; i < start; i++){
                    tmp[i] = points[i];
                }
                for (int i = start; i < tmp.Length; i++)
                {
                    tmp[i] = CreatePoint(i * TimeDelta, AutoDirections);
                }
            }
            Points = tmp;
        }

        public void MovePoint(int i, Vector3 targetPos)
        {
            ControlPoints[i].SetPosition(targetPos);
        }

        public ControlPoint InsertControlPoint(float timestamp) {
            //Debug.Log("Insert ControlPoint at " + timestamp);
            Vector3 pos = CalculatePointPosition(timestamp);
            int index = GetControlPointIndex(timestamp, 1);
            ControlPoint cp = new ControlPoint(Authoring, pos, Authoring.GroundInput); 
            cp.SetDirectionRelativeTo(ControlPoints[index].GetPosition());
            ControlPoints.Insert(index, cp);
            GenerateAllPoints(TimeDelta);
            /*
            UpdateAroundControlPoint(index, UPDATETYPE.INSERT);
            UpdateAroundControlPoint(index + TimeInterval, UPDATETYPE.NONE);
            UpdateAroundControlPoint(index - TimeInterval, UPDATETYPE.NONE);
            */
            return cp;
        }
        
        public ControlPoint AddControlPoint(Vector3 pos)
        {
            ControlPoint cp = new ControlPoint(Authoring, pos, Authoring.GroundInput);
            ControlPoints.Add(cp);
            UpdateControlPointDirection(NumControlPoints-1);
            GenerateAllPoints(TimeDelta);
            //Debug.Log("Added ControlPoint " + NumControlPoints);
            /*
            UpdateAroundControlPoint(GetTotalTime() - TimeInterval, UPDATETYPE.ADD);
            UpdateAroundControlPoint(GetTotalTime() - 2*TimeInterval, UPDATETYPE.NONE);
            */
            return cp;
        }

        public void UpdateControlPointDirection(int index){
            if(ControlPoints.Count < 2 ) return;
            ControlPoint cp = ControlPoints[index];
            ControlPoint cpPrevious = ControlPoints[Mathf.Clamp(index - 1 ,0 , NumControlPoints-1)];
            Vector3 direction = (cp.GetPosition() - cpPrevious.GetPosition()).normalized;
            cpPrevious.SetDirection(direction);
            cp.SetDirection(direction);
        }
        
        public void RemoveControlPoint(int index)
        {
            // Don't delete segment if its the last one remaining
            if (NumControlPoints > 2) {
                ControlPoints[index].RemoveAllModules();
                ControlPoints.RemoveAt(index);
                UpdateControlPointDirection(index - 1);
                GenerateAllPoints(TimeDelta);
            }
        }
        public void RemoveControlPoint(float timestamp)
        {
            if (NumControlPoints > 2) {
                GetControlPoint(timestamp, 0).RemoveAllModules();
                ControlPoints.RemoveAt(GetClosestIndex(timestamp));
                UpdateControlPointDirection(GetClosestIndex(timestamp) - 1);
                GenerateAllPoints(TimeDelta);
            }
        }
        public void RemoveControlPoint(ControlPoint cp)
        {
            if (NumControlPoints > 2) {
                cp.RemoveAllModules();
                ControlPoints.Remove(cp);
                UpdateControlPointDirection(ControlPoints.IndexOf(cp) - 1);
                GenerateAllPoints(TimeDelta);
            }
        }

        public void TransformPathRelativeToTarget(Matrix4x4 target){
            if(NumControlPoints <= 0) return;
            // Transform Transformation 
            Matrix4x4 reference = Matrix4x4.TRS(ControlPoints[0].GetPosition(), Quaternion.LookRotation(ControlPoints[0].GetDirection(), Vector3.up), Vector3.one);
            for(int i=0; i < NumControlPoints; i++){
                ControlPoints[i].TransformFromTo(reference, target);
            }
            GenerateAllPoints(TimeDelta);    
        }

        #endregion

        #region Time Managment
        public int CalculateNumberPoints(){
            return (int)Math.Round(GetTotalTime() / TimeDelta);
        }
        //return the maximum time for each loop or round
        public float GetTotalTime()
        {
            if (isLooping)
            {
                return TimeInterval * NumControlPoints;
            }
            return TimeInterval * (NumControlPoints - 1);
        }
        // make sure that the timestamp is in correct interval between 0f and TotalTime
        public float FilterTimestamp(float timestamp)
        {
            if (isLooping)
            {
                return Mathf.Repeat(timestamp, GetTotalTime());
            }
            return Mathf.Clamp(timestamp, 0f, GetTotalTime());
        }
        //get PivotIndex of given timestamp for ControlPoint
        public int GetClosestIndex(float timestamp)
        {
            return Mathf.FloorToInt(FilterTimestamp(timestamp) / TimeInterval);
        }
        public float GetClosestTimestamp(float timestamp)
        {
            return TimeInterval * GetClosestIndex(timestamp);
        }

        public float GetTimestampByPointIndex(int index)
        {
            return FilterTimestamp(index * TimeDelta);
        }
        public float GetTimestampByControlPoint(ControlPoint cp){
            return ControlPoints.IndexOf(cp) * TimeInterval;
        }
        public int GetControlPointIndex(float timestamp, int offset)
        {
            if (isLooping)
            {
                return Mathf.Clamp((int)Mathf.Repeat(GetClosestIndex(timestamp) + offset, NumControlPoints), 0, NumControlPoints - 1);
            }
            return Mathf.Clamp(GetClosestIndex(timestamp) + offset, 0, NumControlPoints - 1);
        }

        public float GetControlPointTimestamp(float timestamp, int offset)
        {
            if (isLooping)
            {
                return Mathf.Repeat(GetClosestTimestamp(timestamp + TimeInterval * offset), GetTotalTime());
            }
            return Mathf.Clamp(GetClosestTimestamp(timestamp + TimeInterval * offset), 0, GetTotalTime());
        }

        public ControlPoint GetControlPoint(float timestamp, int offset)
        {
            if (isLooping)
            {
                return ControlPoints[Mathf.Clamp((int)Mathf.Repeat(GetClosestIndex(timestamp) + offset, NumControlPoints), 0, NumControlPoints - 1)];
            }
            return ControlPoints[Mathf.Clamp(GetClosestIndex(timestamp) + offset, 0, NumControlPoints - 1)];
        }

        public ControlPoint[] GetCatmullRomControlPoints(float timestamp)
        {
            //get all 4 CP's for CatmullRom calculation
            ControlPoint[] cps = PathUtility.CreateArray(
                GetControlPoint(timestamp, -1),
                GetControlPoint(timestamp, 0),
                GetControlPoint(timestamp, 1),
                GetControlPoint(timestamp, 2));
            return cps;
        }
        public float GetCatmullRomTime(float timestamp)
        {
            float t = FilterTimestamp(timestamp);
            float pivot = GetClosestTimestamp(timestamp);
            if (t < pivot)
            {
                t += GetTotalTime();
            }
            return (t - pivot) / TimeInterval; // [0,1]
        }
        #endregion

        #region Compute Features
        // Features 
        public Vector3 CalculatePointPosition(float timestamp)
        {
            ControlPoint[] cps = GetCatmullRomControlPoints(timestamp);
            float t = GetCatmullRomTime(timestamp);
            return PathUtility.GetCatmullRomVector(t, cps[0].GetPosition(), cps[1].GetPosition(), cps[2].GetPosition(), cps[3].GetPosition());
        }
        public Vector3 CalculatePointDirection(float timestamp)
        {
            ControlPoint[] cps = GetCatmullRomControlPoints(timestamp);
            float t = GetCatmullRomTime(timestamp);
            return PathUtility.GetCatmullRomVector(t, cps[0].GetDirection(), cps[1].GetDirection(), cps[2].GetDirection(), cps[3].GetDirection());
        }
        public Vector3 CalculatePointVelocity(float timestamp, float delta)
        {
            return (CalculatePointPosition(timestamp) - CalculatePointPosition(timestamp - delta)) / delta;
        }
        public float CalculatePointSpeed(float timestamp)
        {
            ControlPoint[] cps = GetCatmullRomControlPoints(timestamp);
            float t = GetCatmullRomTime(timestamp);
            return PathUtility.GetCatmullRomValue(t, cps[0].GetSpeed(), cps[1].GetSpeed(), cps[2].GetSpeed(), cps[3].GetSpeed());
        }
        private float CalculatePointSpeed(float timestamp, float timeinterval)
        {
            float length = 0f;
            Vector3 lastPos = ControlPoints.ToArray()[0].GetPosition();
            for (float i = timestamp; i <= timestamp + timeinterval; i += TimeDelta)
            {
                length += Vector3.Distance(CalculatePointPosition(i), CalculatePointPosition(i + TimeDelta));
            }
            //length = Vector3.Distance(GetPointPositon(timestamp), GetPointPositon(timestamp + timeinterval));
            return length;
        }

        public float[] CalculatePointStyleValues(ControlPoint[] cps, float timestamp)
        {
            float[] styleValues = new float[cps[0].GetModule<StyleActionModule>().GetStyles().Count];
            for(int i=0; i<styleValues.Length; i++){
                styleValues[i] = CalculatePointStyleValue(timestamp, i);
            }
            return styleValues;
        }
        public float CalculatePointStyleValue(float timestamp, int styleOffset)
        {
            ControlPoint[] cps = GetCatmullRomControlPoints(timestamp);
            for(int i=0; i<cps.Length; i++){
                if(!cps[i].HasModule<StyleActionModule>()){
                    Debug.LogWarning("StyleModule wants to be accessed but ControlPoint " + ControlPoints.IndexOf(cps[i]) + " has no StyleActionModule!");
                    return 0f;
                }
            }
            float t = GetCatmullRomTime(timestamp);
            return PathUtility.GetCatmullRomValue(t, cps[0].GetModule<StyleActionModule>().GetStyleValues()[styleOffset], cps[1].GetModule<StyleActionModule>().GetStyleValues()[styleOffset],
            cps[2].GetModule<StyleActionModule>().GetStyleValues()[styleOffset], cps[3].GetModule<StyleActionModule>().GetStyleValues()[styleOffset]);
        }
        #endregion

        #region Internal methods

        /// Calculate time data for closest point on the path from given world point
        TimeOnPathData CalculateClosestTimeBetweenInterpolatedPoints(Vector3 worldPoint)
        {
            float minSqrDst = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;
            int closestSegmentIndexA = 0;
            int closestSegmentIndexB = 0;
            TimeOnPathData controlPointData = CalculateClosestTimeBetweenControlPoints(worldPoint);
            int start = GetPointIndex(controlPointData.previousIndex * TimeInterval);
            int end = GetPointIndex(controlPointData.nextIndex * TimeInterval);

            if(start >= end && isLooping){
                end = Points.Length;
            }

            for (int i = start; i < end; i++)
            {
                int nextI = i + 1;
                if (nextI >= Points.Length)
                {
                    if (isLooping)
                    {
                        nextI %= Points.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                Vector3 closestPointOnSegment = PathUtility.ClosestPointOnLineSegment(worldPoint, Points[i].GetPosition(), Points[nextI].GetPosition());
                float sqrDst = (worldPoint - closestPointOnSegment).sqrMagnitude;
                if (sqrDst < minSqrDst)
                {
                    minSqrDst = sqrDst;
                    closestPoint = closestPointOnSegment;
                    closestSegmentIndexA = i;
                    closestSegmentIndexB = nextI;
                }

            }
            float closestSegmentLength = (Points[closestSegmentIndexA].GetPosition() - Points[closestSegmentIndexB].GetPosition()).magnitude;
            float t = (closestPoint - Points[closestSegmentIndexA].GetPosition()).magnitude / closestSegmentLength;
            return new TimeOnPathData(closestSegmentIndexA, closestSegmentIndexB, t);
        }

        
        // Line Segment!
        TimeOnPathData CalculateClosestTimeBetweenControlPoints(Vector3 worldPoint)
        {
            float minSqrDst = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;
            int closestSegmentIndexA = 0;
            int closestSegmentIndexB = 0;

            for (int i = 0; i < NumControlPoints; i++)
            {
                int nextI = i + 1;
                if (nextI >= NumControlPoints)
                {
                    if (isLooping)
                    {
                        nextI %= NumControlPoints;
                    }
                    else
                    {
                        break;
                    }
                }

                Vector3 closestPointOnSegment = PathUtility.ClosestPointOnLineSegment(worldPoint, ControlPoints[i].GetPosition(), ControlPoints[nextI].GetPosition());
                float sqrDst = (worldPoint - closestPointOnSegment).sqrMagnitude;
                if (sqrDst < minSqrDst)
                {
                    minSqrDst = sqrDst;
                    closestPoint = closestPointOnSegment;
                    closestSegmentIndexA = i;
                    closestSegmentIndexB = nextI;
                }
            }

            float closestSegmentLength = (ControlPoints[closestSegmentIndexA].GetPosition() - ControlPoints[closestSegmentIndexB].GetPosition()).magnitude;
            float t = (closestPoint - ControlPoints[closestSegmentIndexA].GetPosition()).magnitude / closestSegmentLength;
            return new TimeOnPathData(closestSegmentIndexA, closestSegmentIndexB, t);
        }
        

        public struct TimeOnPathData
        {
            public readonly int previousIndex;
            public readonly int nextIndex;
            public readonly float percentBetweenIndices;

            public TimeOnPathData(int prev, int next, float percentBetweenIndices)
            {
                this.previousIndex = prev;
                this.nextIndex = next;
                this.percentBetweenIndices = percentBetweenIndices;
            }
        }
        #endregion
    }
}