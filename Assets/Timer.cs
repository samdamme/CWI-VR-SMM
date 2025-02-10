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
        outputfile = Application.persistentDataPath+"/timing/"+starttime+".txt";
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

    public void mark()
    {
        writeTimer("MARKER");
    }

    public void pause()
    {
        writeTimer("PAUSED");
    }

    public void restart()
    {
        writeTimer("RESTARTED");
    }

    private void OnDestroy()
    {
        timer.Stop();
        writeTimer("END");
        writer.Close();
    }
}
