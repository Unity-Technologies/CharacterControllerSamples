using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace OnlineFPS
{
    public class StatsPanel : MonoBehaviour
    {
        public UIDocument UIDocument;

        public float StatsPollDuration = 1f;

        private bool _statsEnabled;
        private float _accumulatedDeltaTimes;
        private int _accumulatedFPSFrames;
        private float _maxFPSDeltaTime;
        private float _deltaAvg;
        private float _deltaWorst;

        private VisualElement _uiRoot;
        private Label _avgFramerateLabel;
        private Label _worstFramerateLabel;
        private Label _pingLabel;

        private World _observedWorld;

        private void Awake()
        {
            _statsEnabled = true;

            _uiRoot = UIDocument.rootVisualElement;
            _avgFramerateLabel = _uiRoot.Query<Label>("FramerateAvg");
            _worstFramerateLabel = _uiRoot.Query<Label>("FramerateMin");
            _pingLabel = _uiRoot.Query<Label>("Ping");
        }

        void Update()
        {
            if (_statsEnabled)
            {
                // Update observed world
                if (_observedWorld == null || !_observedWorld.IsCreated)
                {
                    _observedWorld = null;
                    for (int i = 0; i < World.All.Count; i++)
                    {
                        World tmpWorld = World.All[i];
                        if (_observedWorld == null)
                        {
                            _observedWorld = tmpWorld;
                        }
                        else
                        {
                            if (tmpWorld == World.DefaultGameObjectInjectionWorld)
                            {
                                _observedWorld = tmpWorld;
                            }

                            if (tmpWorld.IsClient())
                            {
                                _observedWorld = tmpWorld;
                                break;
                            }
                        }
                    }
                }

                if (_observedWorld != null && _observedWorld.IsCreated)
                {
                    _accumulatedDeltaTimes += _observedWorld.Time.DeltaTime;

                    // Framerate
                    {
                        _maxFPSDeltaTime = math.max(_maxFPSDeltaTime, _observedWorld.Time.DeltaTime);
                        _accumulatedFPSFrames++;
                        _deltaAvg = _accumulatedDeltaTimes / _accumulatedFPSFrames;
                        _deltaWorst = _maxFPSDeltaTime;
                    }

                    // Update stats display
                    if (_accumulatedDeltaTimes >= StatsPollDuration)
                    {
                        // Framerate
                        {
                            _accumulatedFPSFrames = 0;
                            _maxFPSDeltaTime = 0f;

                            _avgFramerateLabel.text =
                                $"FPS avg: {String.Format("{0:0}", (1f / _deltaAvg))} ({String.Format("{0:0.0}", _deltaAvg * 1000f)}ms)";
                            _worstFramerateLabel.text =
                                $"FPS min: {String.Format("{0:0}", (1f / _deltaWorst))} ({String.Format("{0:0.0}", _deltaWorst * 1000f)}ms)";
                        }

                        // Ping
                        {
                            EntityQuery networkAckQuery = new EntityQueryBuilder(Allocator.Temp)
                                .WithAll<NetworkSnapshotAck>()
                                .Build(_observedWorld.EntityManager);
                            if (networkAckQuery.HasSingleton<NetworkSnapshotAck>())
                            {
                                NetworkSnapshotAck networkAck = networkAckQuery.GetSingleton<NetworkSnapshotAck>();
                                _pingLabel.text = $"Ping: {(int)networkAck.EstimatedRTT}";
                            }
                            else
                            {
                                _pingLabel.text = $"Ping: ---";
                            }
                        }

                        _accumulatedDeltaTimes -= StatsPollDuration;
                    }
                }
            }
        }
    }
}
