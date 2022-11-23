using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System;

public struct FramerateCalculator
{
    private int _framesCount;
    private float _framesDeltaSum;
    private float _minDeltaTimeForAvg;
    private float _maxDeltaTimeForAvg;
    private string[] _framerateStrings;

    public void Initialize()
    {
        _minDeltaTimeForAvg = Mathf.Infinity;
        _maxDeltaTimeForAvg = Mathf.NegativeInfinity;
        _framerateStrings = new string[1001];
        for (int i = 0; i < _framerateStrings.Length; i++)
        {
            if (i >= _framerateStrings.Length - 1)
            {
                _framerateStrings[i] = i.ToString() + "+" + " (<" + (1000f / (float)i).ToString("F") + "ms)";
            }
            else
            {
                _framerateStrings[i] = i.ToString() + " (" + (1000f / (float)i).ToString("F") + "ms)";
            }
        }
    }

    public void Update()
    {
        // Regular frames
        _framesCount++;
        _framesDeltaSum += Time.deltaTime;

        // Max and min
        if (Time.deltaTime < _minDeltaTimeForAvg)
        {
            _minDeltaTimeForAvg = Time.deltaTime;
        }

        if (Time.deltaTime > _maxDeltaTimeForAvg)
        {
            _maxDeltaTimeForAvg = Time.deltaTime;
        }
    }

    private string GetNumberString(int fps)
    {
        if (fps < _framerateStrings.Length - 1 && fps >= 0)
        {
            return _framerateStrings[fps];
        }
        else
        {
            return _framerateStrings[_framerateStrings.Length - 1];
        }
    }

    public void PollFramerate(out string avg, out string worst, out string best)
    {
        avg = GetNumberString(Mathf.RoundToInt(1f / (_framesDeltaSum / _framesCount)));
        worst = GetNumberString(Mathf.RoundToInt(1f / _maxDeltaTimeForAvg));
        best = GetNumberString(Mathf.RoundToInt(1f / _minDeltaTimeForAvg));

        _framesDeltaSum = 0f;
        _framesCount = 0;
        _minDeltaTimeForAvg = Mathf.Infinity;
        _maxDeltaTimeForAvg = Mathf.NegativeInfinity;
    }
}

public class FramerateMenuManager : MonoBehaviour
{
    [Header("Components")] public Canvas MainCanvas;
    public Text AvgFPS;
    public Text WorstFPS;
    public Text BestFPS;

    [Header("Misc")] public float FPSPollRate = 1f;

    private FramerateCalculator _framerateCalculator = default;
    private float _lastTimePolledFPS = float.MinValue;
    private bool _hasVSync = false;

    void Start()
    {
        _framerateCalculator.Initialize();
        UpdateRenderSettings();
    }

    void Update()
    {
        // show hide
        if (Input.GetKeyDown(KeyCode.F1))
        {
            MainCanvas.gameObject.SetActive(!MainCanvas.gameObject.activeSelf);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            _hasVSync = !_hasVSync;
            UpdateRenderSettings();
        }

        // FPS
        _framerateCalculator.Update();
        if (Time.time >= _lastTimePolledFPS + FPSPollRate)
        {
            _framerateCalculator.PollFramerate(out string avg, out string worst, out string best);
            AvgFPS.text = avg;
            WorstFPS.text = worst;
            BestFPS.text = best;

            _lastTimePolledFPS = Time.time;
        }
    }

    private void UpdateRenderSettings()
    {
        QualitySettings.vSyncCount = _hasVSync ? 1 : 0;
    }
}