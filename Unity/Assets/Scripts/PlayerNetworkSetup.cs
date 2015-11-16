﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Assets.Scripts;

public class PlayerNetworkSetup : NetworkBehaviour {

    [SerializeField]
    public Camera PlayerCamera;
    [SerializeField]
    public AudioListener audioListener;

    /*
    [SyncVar]
    public Transform rotation;
    */ 

    public GameObject spawnLocation;
    //public NetworkTransformChild transformChild;
    public Transform headTransform;
    //public GameObject playerUI;
     

    //public Camera PlayerCamera;
    //public AudioListener audioListener;

    private bool rebindOnce = false;

	// Use this for initialization
	void Start ()
    {
        //If this script started is the local player
        if(isLocalPlayer)
        {
            //Check if the scene currently is the Mansion
            if (Application.loadedLevelName.Contains("Mansion"))
            {
                //Disable the MainCamera which may be by default in the scene
                //GameObject.Find("SceneCamera").SetActive(false);

                //Activate the Player Script on the player
                GetComponent<Assets.Scripts.Player>().enabled = true;

                //Turn on First Person Camera
                PlayerCamera.gameObject.SetActive(true);

                //Activate Audio Listener
                audioListener.enabled = true;

                //Set Network Animator
                for (int i = 0; i < GetComponent<Animator>().parameterCount; i++)
                    GetComponent<NetworkAnimator>().SetParameterAutoSend(i, true);
                
                //Setup player stats
                setupPlayerStats();

                //Add Head NetworkTransform
                //GetComponent<NetworkTransformChild>().target = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);

                //Set player to SpawnLocation
                spawnLocation = GameObject.Find("SpawnLocation");
                gameObject.transform.position = spawnLocation.transform.position + new Vector3(Random.Range(-1F, 1F), 0, Random.Range(-1F, 1F));

                //Disable Spawnlocation
                spawnLocation.SetActive(false);
            }
        }
	}
	
	// Update is called once per frame
	void Update () 
    {
	    if (!rebindOnce)
        {
            gameObject.GetComponent<Animator>().Rebind();
            gameObject.GetComponent<Player>().headRotateTransform = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            rebindOnce = true;
        }

        /*
        if( isLocalPlayer )
        {
            rotation = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
        }
        */
	}

    //Setup the player stats and perks upon start
    public void setupPlayerStats()
    {
        //Check if file exists. If so, read it and set stats
        if (File.Exists(Application.persistentDataPath + "/playerInfo.dat"))
        { //If save file exists
            //READ FILE AND SET INTEGERS CORRECTLY
            BinaryFormatter bf = new BinaryFormatter();
            //FileStream file = File.Open(Application.persistentDataPath + "/playerInfo.dat", FileMode.Open);
            FileStream file = File.Open("playerInfo.dat", FileMode.Open);

            //Deserialize game so it can be understood
            PlayerData data = (PlayerData)bf.Deserialize(file);

            //Close file since we have loaded the file into game
            file.Close();

            //Set variables from load
            Player player = gameObject.GetComponent<Player>();
            player.Brawn = new Stat(data.brawn);
            player.Speed = new Stat(data.speed);
            player.Intellect = new Stat(data.intellect);
            player.Willpower = new Stat(data.willpower);
            Perk perk = Resources.LoadAll<Perk>("Data/Perks").FirstOrDefault(p => p.Name == data.perk);
            if (perk != null)
                player.Perks.Add(perk);

            //Model
            /*
            string modelName = data.model;
            string meIdenity = gameObject.name;
            CmdTellServerTheModel(modelName, meIdenity);
            */

            Transform models = gameObject.transform.FindChild("Model");
            for (int i = 0; i < models.childCount; i++ )
            {
                var child = models.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
            GameObject model = Instantiate(Resources.Load<GameObject>("CharacterModels/" + data.model));
            model.transform.SetParent(models, false);

            //Setup name DISABLED CAUSE IT DOESN'T SYNC ACROSS TO OTHERS YET ON GAMECLIENT.CS
            //gameObject.GetComponent<gameClient>().uniqueName = data.name;

            Debug.Log("Character Loaded: " + Application.persistentDataPath + "/playerInfo.dat");
        }
        else
        {
            Debug.Log("ERROR LOADING SAVE FILE FROM LOAD FILE()");
        }
    }

    [Command]
    private void CmdTellServerTheModel(string modelName, string playerID)
    {
        GameObject player = GameObject.Find(playerID);
        var conn = player.GetComponent<NetworkIdentity>().connectionToClient;
        var connID = player.GetComponent<NetworkIdentity>().netId;

        Transform models = player.transform.FindChild("Model");
        for (int i = 0; i < models.childCount; i++)
        {
            var child = models.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
        
        GameObject model = Instantiate(Resources.Load<GameObject>("CharacterModels/Models" + modelName));

        NetworkServer.ReplacePlayerForConnection(conn, model, 0);

        model.transform.SetParent(models, false);
    }
}
