using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PositionTracker : MonoBehaviour
{
    [Header("Recording Settings")]
    public string experiment = "ours"; // our experiment case - ours, w/o thrust vector control, ect...
    public string interpolation = "b-spline"; // interpolation method for caculating travel length - b-spline, linear, nurbs
    [Range(0.005f, 10f)]
    public float recordSec = 0.1f; // recording x and y every recordSec
    public string csvFilePath = "position_data.csv"; // csv file path for us. I HIGHLY recommend to change it to where you want.
    public int maxRecordedPositions = 200000; // max limit - worry of data amount

    [Header("References")]
    public Rigidbody rb;
    public TimerBehave timerBehave;

    public List<Vector2> recordedPositions = new List<Vector2>();
    private float recordTimer = 0f;
    private bool isTracking = false;

    // Returns the most recent 'count' positions.
    public List<Vector2> GetRecentPositions(int count)
    {
        int start = Mathf.Max(0, recordedPositions.Count - count);
        int numElements = Mathf.Min(count, recordedPositions.Count);
        return recordedPositions.GetRange(start, numElements);
    }

    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        if (timerBehave.inControl && !isTracking)
        {
            StartTracking();
        }

        if (isTracking && timerBehave.inControl)
        {
            recordTimer += Time.deltaTime;
            if (recordTimer >= recordSec)
            {
                RecordPosition();
                recordTimer = 0f;
            }
        }
    }

    public void StartTracking()
    {
        isTracking = true;
        recordedPositions.Clear();
        recordTimer = 0f;
    }

    public void OnGameFinished()
    {
        if (!isTracking) return;

        isTracking = false;
        float distance = CalculateDistance();
        float completionTime = timerBehave.timer; 
        SaveToCSV(distance, completionTime); 
    }

    private void RecordPosition()
    {
        if (recordedPositions.Count >= maxRecordedPositions)
        {
            Debug.LogWarning($"Maximum recorded positions ({maxRecordedPositions}) reached. Stopping recording.");
            isTracking = false;
            return;
        }

        Vector2 pos = new Vector2(rb.position.x, rb.position.z);
        recordedPositions.Add(pos);
    }

    private float CalculateDistance()
    {
        if (recordedPositions.Count < 2)
            return 0f;

        switch (interpolation.ToLower())
        {
            case "linear":
                return CalculateLinearDistance();
            case "b-spline":
                return CalculateBSplineDistance();
            case "nurbs":
                return CalculateNURBSDistance();
            default:
                Debug.LogWarning($"Unknown interpolation method: {interpolation}. Using b-spline.");
                return CalculateBSplineDistance();
        }
    }

    private float CalculateLinearDistance()
    {
        float totalDistance = 0f;
        for (int i = 0; i < recordedPositions.Count - 1; i++)
        {
            totalDistance += Vector2.Distance(recordedPositions[i], recordedPositions[i + 1]);
        }
        return totalDistance;
    }

    private float CalculateBSplineDistance()
    {
        if (recordedPositions.Count < 4)
            return CalculateLinearDistance();

        List<Vector2> splinePoints = new List<Vector2>();
        int segments = 10;

        for (int i = 0; i < recordedPositions.Count - 3; i++)
        {
            Vector2 p0 = recordedPositions[i];
            Vector2 p1 = recordedPositions[i + 1];
            Vector2 p2 = recordedPositions[i + 2];
            Vector2 p3 = recordedPositions[i + 3];

            for (int j = 0; j < segments; j++)
            {
                float t = j / (float)segments;
                Vector2 point = CalculateCubicBSplinePoint(p0, p1, p2, p3, t);
                splinePoints.Add(point);
            }
        }

        float totalDistance = 0f;
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            totalDistance += Vector2.Distance(splinePoints[i], splinePoints[i + 1]);
        }
        return totalDistance;
    }

    private Vector2 CalculateCubicBSplinePoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float b0 = (-t3 + 3 * t2 - 3 * t + 1) / 6f;
        float b1 = (3 * t3 - 6 * t2 + 4) / 6f;
        float b2 = (-3 * t3 + 3 * t2 + 3 * t + 1) / 6f;
        float b3 = t3 / 6f;

        return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
    }

    private float CalculateNURBSDistance()
    {
        if (recordedPositions.Count < 4)
            return CalculateLinearDistance();

        List<Vector2> nurbsPoints = new List<Vector2>();
        int segments = 10;
        List<float> weights = Enumerable.Repeat(1f, recordedPositions.Count).ToList();

        int degree = 3;
        List<float> knots = GenerateKnotVector(recordedPositions.Count, degree);

        for (int i = 0; i < recordedPositions.Count - 3; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                float t = knots[i + degree] + (j / (float)segments) * (knots[i + degree + 1] - knots[i + degree]);
                Vector2 point = CalculateNURBSPoint(t, degree, knots, weights);
                nurbsPoints.Add(point);
            }
        }

        float totalDistance = 0f;
        for (int i = 0; i < nurbsPoints.Count - 1; i++)
        {
            totalDistance += Vector2.Distance(nurbsPoints[i], nurbsPoints[i + 1]);
        }
        return totalDistance;
    }

    private List<float> GenerateKnotVector(int numControlPoints, int degree)
    {
        int numKnots = numControlPoints + degree + 1;
        List<float> knots = new List<float>();

        for (int i = 0; i < numKnots; i++)
        {
            if (i <= degree)
                knots.Add(0f);
            else if (i >= numKnots - degree - 1)
                knots.Add(1f);
            else
                knots.Add((float)(i - degree) / (numControlPoints - degree));
        }

        return knots;
    }

    private Vector2 CalculateNURBSPoint(float t, int degree, List<float> knots, List<float> weights)
    {
        Vector2 point = Vector2.zero;
        float weightSum = 0f;

        for (int i = 0; i < recordedPositions.Count; i++)
        {
            float basis = CalculateBasisFunction(i, degree, t, knots);
            float weightedBasis = basis * weights[i];
            point += recordedPositions[i] * weightedBasis;
            weightSum += weightedBasis;
        }

        return weightSum > 0 ? point / weightSum : Vector2.zero;
    }

    private float CalculateBasisFunction(int i, int degree, float t, List<float> knots)
    {
        if (degree == 0)
        {
            return (knots[i] <= t && t < knots[i + 1]) ? 1f : 0f;
        }

        float left = 0f;
        float right = 0f;

        float denomLeft = knots[i + degree] - knots[i];
        if (denomLeft > 0)
            left = ((t - knots[i]) / denomLeft) * CalculateBasisFunction(i, degree - 1, t, knots);

        float denomRight = knots[i + degree + 1] - knots[i + 1];
        if (denomRight > 0)
            right = ((knots[i + degree + 1] - t) / denomRight) * CalculateBasisFunction(i + 1, degree - 1, t, knots);

        return left + right;
    }

    private void SaveToCSV(float distance, float completionTime)
    {
        bool fileExists = File.Exists(csvFilePath);
        
        using (StreamWriter writer = new StreamWriter(csvFilePath, true))
        {
            if (!fileExists)
            {
                writer.WriteLine("test,distance,time,location");
            }

            string locationData = "";
            for (int i = 0; i < recordedPositions.Count; i++)
            {
                locationData += $"{recordedPositions[i].x},{recordedPositions[i].y}";
                if (i < recordedPositions.Count - 1)
                    locationData += ",";
            }

            writer.WriteLine($"{experiment},{distance},{completionTime},{locationData}");
        }

        Debug.Log($"Data saved to {csvFilePath}. Distance: {distance}, Time: {completionTime}");
    }
}