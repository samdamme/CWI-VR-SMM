using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class folder_creator : MonoBehaviour
{
    private void Awake()
    {
        string pt_folder = Application.persistentDataPath+"/position_tracking/";
        FileInfo file = new FileInfo(pt_folder);
        file.Directory.Create();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
