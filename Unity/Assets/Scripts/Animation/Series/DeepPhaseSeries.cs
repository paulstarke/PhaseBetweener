using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class DeepPhaseSeries : ComponentSeries {
    public int Channels;
    public Vector2[][] Phases;
    public float[][] Amplitudes;
    public float[][] Frequencies;
    private float Max = float.MinValue;
    private UltiDraw.GUIRect Rect = new UltiDraw.GUIRect(0.875f, 0.15f, 0.2f, 0.15f);
    public DeepPhaseSeries(TimeSeries global, int channels) : base(global) {
        Channels = channels;
        Phases = new Vector2[Samples.Length][];
        Amplitudes = new float[Samples.Length][];
        Frequencies = new float[Samples.Length][];
        for(int i=0; i<Samples.Length; i++) {
            Phases[i] = new Vector2[channels];
            Phases[i].SetAll(Vector2.up);
            Amplitudes[i] = new float[channels];
            Frequencies[i] = new float[channels];
        }
    }

    // public void ComputeEncoding(Vector2[] phases, float[] amplitudes, float[] frequencies) {
    //     for(int c=0; c<Channels; c++) {
    //         Vector2 phase = phases[c];
    //         float amplitude = amplitudes[c];
    //         float frequency = frequencies[c];
    //         for(int i=0; i<Samples.Length; i++) {
    //             Amplitudes[i][c]
    //         }
    //     }
    // }
    public void UpdateAlignment(float[] values, float stability, float deltaTime, float minAmplitude=0f) {
        int pivot = 0;
        for(int i=PivotKey; i<KeyCount; i++) {
            int index = GetKey(i).Index;
            for(int b=0; b<Channels; b++) {
                Vector2 p = Phases[index][b];
                // float a = Amplitudes[index][b];
                // float f = Frequencies[index][b];
                Vector2 next = new Vector2(values[pivot+0], values[pivot+1]).normalized;
                float amp = Mathf.Abs(values[pivot+2]);
                amp = Mathf.Max(amp, minAmplitude);
                float freq = Mathf.Abs(values[pivot+3]);
                pivot += 4;
                Vector2 update = Quaternion.AngleAxis(-freq*360f*deltaTime, Vector3.forward) * p;
                Phases[index][b] = Vector3.Slerp(update.normalized, next.normalized, stability).ZeroZ().normalized;
                Amplitudes[index][b] = amp;
                Frequencies[index][b] = freq;
                Max = Mathf.Max(Max, Mathf.Min(1.0f,amp));
            }
        }
    }
    public float[] GetAlignment() {
        int pivot = 0;
        float[] alignment = new float[Channels * KeyCount * 2];
        for(int k=0; k<KeyCount; k++) {
            int index = GetKey(k).Index;
            for(int b=0; b<Channels; b++) {
                Vector2 phase = Amplitudes[index][b] * Phases[index][b];
                alignment[pivot] = phase.x; pivot += 1;
                alignment[pivot] = phase.y; pivot += 1;
            }
        }
        return alignment;
    }
    
    public float[] GetUpdate() {
        int pivot = 0;
        float[] update = new float[Channels * (FutureKeys+1) * 4];
        for(int k=PivotKey; k<KeyCount; k++) {
            for(int b=0; b<Channels; b++) {
                Vector2 phase = Phases[k][b];
                float amp = Amplitudes[k][b];
                float freq = Frequencies[k][b];
                phase *= amp;
                update[pivot] = phase.x; pivot += 1;
                update[pivot] = phase.y; pivot += 1;
                update[pivot] = amp; pivot += 1;
                update[pivot] = freq; pivot += 1;
            }
        }
        return update;
    }
    public override void Increment(int start, int end) {
        for(int i=start; i<end; i++) {
            for(int j=0; j<Channels; j++) {
                Phases[i][j] = Phases[i+1][j];
                Amplitudes[i][j] = Amplitudes[i+1][j];
                Frequencies[i][j] = Frequencies[i+1][j];
            }
        }
    }
    public override void Interpolate(int start, int end) {
        for(int i=start; i<end; i++) {
            float weight = (float)(i % Resolution) / (float)Resolution;
            int prevIndex = GetPreviousKey(i).Index;
            int nextIndex = GetNextKey(i).Index;
            for(int j=0; j<Channels; j++) {
                Vector2 prev = Phases[prevIndex][j];
                Vector2 next = Phases[nextIndex][j];
                float update = Utility.PhaseUpdate(Utility.PhaseValue(prev), Utility.PhaseValue(next));
                Phases[i][j] = Utility.PhaseVector(Utility.PhaseValue(Quaternion.AngleAxis(-update*360f*weight, Vector3.forward) * prev.normalized));
                Amplitudes[i][j] = Mathf.Lerp(Amplitudes[prevIndex][j], Amplitudes[nextIndex][j], weight);
                Frequencies[i][j] = Mathf.Lerp(Frequencies[prevIndex][j], Frequencies[nextIndex][j], weight);
            }
        }
    }
    public override void GUI(Camera camera) {
        if(DrawGUI) {
        
        }
    }
    public static void DrawPhaseState(Vector2 center, float radius, Vector2[] manifold, int channels, float max) {
        float[] amplitudes = new float[manifold.Length];
        Vector2[] phases = new Vector2[manifold.Length];
        for(int i=0; i<manifold.Length; i++) {
            amplitudes[i] = manifold[i].magnitude;
            phases[i] = manifold[i].normalized;
        }
        DrawPhaseState(center, radius, amplitudes, phases, channels, max);
    }
    public static void DrawPhaseState(Vector2 center, float radius, float[] amplitudes, Vector2[] phases, int channels, float max) {
        float outerRadius = radius;
        float innerRadius = 2f*Mathf.PI*outerRadius/(channels+1);
        float amplitude = max == float.MinValue ? 1f : max;
        UltiDraw.Begin();
        UltiDraw.GUICircle(center, 1.05f*2f*outerRadius, UltiDraw.White);
        UltiDraw.GUICircle(center, 2f*outerRadius, UltiDraw.BlackGrey);
        for(int i=0; i<channels; i++) {
            float activation = amplitudes[i].Normalize(0f, max, 0f, 1f);
            Color color = UltiDraw.GetRainbowColor(i, channels).Darken(0.5f);
            float angle = Mathf.Deg2Rad*360f*i.Ratio(0, channels);
            Vector2 position = center + outerRadius * new Vector2(Mathf.Sin(angle), UltiDraw.AspectRatio() * Mathf.Cos(angle));
            UltiDraw.GUILine(center, position, 0f, activation*innerRadius, UltiDraw.GetRainbowColor(i, channels).Opacity(activation));
            UltiDraw.GUICircle(position, innerRadius, color);
            UltiDraw.PlotCircularPivot(position, 0.9f*innerRadius, 360f*Utility.PhaseValue(phases[i]), amplitudes[i].Normalize(0f, amplitude, 0f, 1f), UltiDraw.White, UltiDraw.Black);
        }
        UltiDraw.End();
    }
    private Vector2[] Vectors;
    public override void Draw(Camera camera) {
        if(DrawScene) {
            DrawPhaseState(new Vector2(0.9f, 0.7875f + 0.05f), 0.05f, Amplitudes[Pivot], Phases[Pivot], Channels, Max);
        }
        // if(DrawScene) {
        //     UltiDraw.Begin();
        //     float min = 0.05f;
        //     float max = 0.2f;
        //     float w = 0.25f;
        //     float amplitude = Max == float.MinValue ? 1f : Max;
        //     float h = (max-min)/Channels;
        //     // Vector2[][] phases = Phases.GetTranspose();
        //     // float[][] amplitudes = Amplitudes.GetTranspose();
        //     // float[][] frequencies = Frequencies.GetTranspose();
        //     for(int i=0; i<Channels; i++) {
        //         float ratio = i.Ratio(0, Channels-1);
        //         Vectors = Vectors.Validate(Samples.Length);
        //         for(int j=0; j<Vectors.Length; j++) {
        //             Vectors[j] = Amplitudes[j][i] * Phases[j][i];
        //         }
        //         UltiDraw.PlotFunctions(new Vector2(0.5f, ratio.Normalize(0f, 1f, min+h/2f, max-h/2f)), new Vector2(w, h), Vectors, -amplitude, amplitude, thickness:0.001f, backgroundColor:UltiDraw.Black, lineColors:new Color[]{Color.white, Color.white}); //lineColors:new Color[]{UltiDraw.Magenta, UltiDraw.Cyan});
        //         // UltiDraw.PlotFunctions(
        //         //     new Vector2(0.5f, ratio.Normalize(0f, 1f, min+h/2f, max-h/2f)), 
        //         //     new Vector2(0.75f, h), 
        //         //     vectors, 
        //         //     -amplitude, 
        //         //     amplitude, 
        //         //     backgroundColor:UltiDraw.Black, 
        //         //     lineColors:new Color[]{UltiDraw.White, UltiDraw.White}
        //         // );
        //         //Phases
        //         float[] a = new float[Samples.Length];
        //         float[] p = new float[Samples.Length];
        //         Color[] c = new Color[Samples.Length];
        //         for(int j=0; j<Samples.Length; j++) {
        //             p[j] = Utility.PhaseValue(Phases[j][i]);
        //             a[j] = Amplitudes[j][i];
        //             c[j] = UltiDraw.White.Opacity(a[j].Normalize(0f, amplitude, 0f, 1f));
        //         }
        //         UltiDraw.PlotBars(new Vector2(0.5f, ratio.Normalize(0f, 1f, min+h/2f, max-h/2f)), new Vector2(0.75f, h), p, 0f, 1f, backgroundColor:UltiDraw.Transparent, barColors:c);
        //         // UltiDraw.PlotBars(new Vector2(0.25f, ratio.Normalize(0f, 1f, max+h/2f, max+(max-min)-h/2f)), new Vector2(0.25f, h), p, 0f, 1f, barColors:c);
        //         // //Amplitudes
        //         // UltiDraw.PlotFunction(new Vector2(0.3f, ratio.Normalize(0f, 1f, max+h/2f, max+(max-min)-h/2f)), new Vector2(0.35f, h), a, 0f, amplitude);
        //         // //Frequencies
        //         // UltiDraw.PlotFunction(new Vector2(0.7f, ratio.Normalize(0f, 1f, max+h/2f, max+(max-min)-h/2f)), new Vector2(0.35f, h), frequencies[i], 0f, 3.25f);
        //     }
        //     UltiDraw.End();
        // }
    }
}