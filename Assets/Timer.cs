using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using VRT.Core;

public class Timer : MonoBehaviour
{
    private Stopwatch timer;
    private String starttime;
    private String outputfile;
    private StreamWriter writer;

    // Start is called before the first frame update
    void Start()
    {
        starttime = DateTime.Now.ToString().Replace("/", "_").Replace(" ", "_").Replace(":", "_");
        outputfile = "./CWI-VR-SMM_Data/timing/"+starttime+".txt";
        FileInfo file = new FileInfo(outputfile);
        file.Directory.Create();
        writer = new StreamWriter(outputfile);
        timer = new Stopwatch();
        timer.Start();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void writeTimer(String label="")
    {
        TimeSpan time = timer.Elapsed;
        string elapsedTime = String.Format("{0:00}:{1:00}.{2:00}", time.Minutes, time.Seconds, time.Milliseconds);
        writer.WriteLine(label+": "+elapsedTime);
    }

    public void buttonPress()
    {
        writeTimer("BUTTON_PRESS");
    }
    private void OnDestroy()
    {
        UnityEngine.Debug.Log("OnDestroy()");
        timer.Stop();
        writeTimer("DESTROY");
        writer.Close();
    }
}
