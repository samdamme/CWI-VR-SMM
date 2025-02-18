using VRT.Orchestrator.Wrapping;
using VRT.Pilots.Common;
using VRT.Core;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using System.Collections;
using UnityEngine;

public class SMMController : PilotController
{
    private int pc_latency_ms;
    private int voice_latency_ms;
    private bool isMaster;
    private string configFile = "config.json";

    public override void Start()
    {
        base.Start();
        StartCoroutine(SetLatencies());
    }

    IEnumerator SetLatencies() {
        UnityEngine.Debug.Log("SetLatencies");
        yield return new WaitForSeconds(1);
        UnityEngine.Debug.Log("SetLatencies - after wait");
        isMaster = OrchestratorController.Instance.UserIsMaster;
        /*try
        {*/
            string json = File.ReadAllText(configFile);
            var config = JsonSerializer.Deserialize<JsonElement>(json);
            var latencies = config.GetProperty("Latencies").EnumerateArray().ToList();

            if (isMaster)
            
            {
                var latencyTuple = latencies[0];
                pc_latency_ms = latencyTuple.GetProperty("pc_latency_ms_master").GetInt32();
                voice_latency_ms = latencyTuple.GetProperty("voice_latency_ms_master").GetInt32();
            }
            else { 
                var latencyTuple = latencies[1];
                pc_latency_ms = latencyTuple.GetProperty("pc_latency_ms_slave").GetInt32();
                voice_latency_ms = latencyTuple.GetProperty("voice_latency_ms_slave").GetInt32();
            }

            GameObject SelfGO = SessionPlayersManager.Instance.localPlayer;
            GameObject OtherGO = null;

            foreach (PlayerNetworkControllerBase pncb in SessionPlayersManager.Instance.AllUsers)
            {
                GameObject go = pncb.BodyTransform.gameObject;
                if (go != SelfGO)
                {
                    if (OtherGO != null)
                    {
                        Debug.LogError($"VqegController: multiple other users: {go} and {OtherGO}");
                    }
                    OtherGO = go;
                }
            }

            VRTSynchronizer sync = OtherGO.GetComponentInChildren<VRTSynchronizer>();
            UnityEngine.Debug.Log("SMMController: VRTSynchronizer: " + sync);
            sync.requestNonAudioBehindMs = pc_latency_ms;
            sync.requestAudioBehindMs = voice_latency_ms;
            UnityEngine.Debug.Log("SMMController: pc_latency_ms: " + pc_latency_ms + " voice_latency_ms: " + voice_latency_ms);
        /*}
        catch (Exception e)
        {
            UnityEngine.Debug.Log($"Error reading JSON file: {e.Message}");
        }*/
        
    }
}
