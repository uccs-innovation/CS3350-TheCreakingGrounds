﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;
using Assets.Scripts.Menu;
using UnityEngine.Networking;

namespace Assets.Scripts
{
    public class Player : NetworkBehaviour
    {
        public static GameObject Instance;

        //base stats
        public Stat Brawn;
        public Stat Speed;
        public Stat Intellect;
        public Stat Willpower;

        //vital stats
        public Stat Wounds;
        public Stat Traumas;
        int lastWounds;
        int lastTraumas;

        //character perk
        public List<Perk> Perks;

        //inventory
        public Dictionary<InventoryItem, int> Inventory = new Dictionary<InventoryItem,int>();

        //derrived stats
        [NonSerialized]
        public Stat Stamina;

        [NonSerialized]
        public InventoryItem EquippedItem1;
        [NonSerialized]
        public InventoryItem EquippedItem2;

        //
        //below is movement and state info vars
        //
        public RaycastHit ReticleInfo { get; protected set; }

        [NonSerialized]
        public GameObject reticleObject;

        [NonSerialized]
        public float headRotate;

        [NonSerialized]
        public float headPivot;

        [NonSerialized]
        public float camPivot;

        public bool isDoll = false;
        public bool isInMenu = false;

        public Camera cam;
        public Animator animationController;
        public Transform headRotateTransform;
        public float headClampX = 90f;
        public float headClampY = 45f;
        public float camClampY = 85f;

        private Quaternion headHolder = new Quaternion();

        public UIManager UIPrefab;

        [NonSerialized]
        public UIManager UI;

        public bool IsSprinting { get; protected set; }
        public bool IsWinded { get; protected set; }

        public void ShowMouse()
        {
            gameObject.GetComponent<disableMouse>().ShowCursor();
            isInMenu = true;
        }

        public void HideMouse()
        {
            gameObject.GetComponent<disableMouse>().HideCursor();
            isInMenu = false;
        }

        public virtual void Start()
        {
            Instance = gameObject;
            sbyte stamina = (sbyte)Mathf.RoundToInt(Mathf.Pow((Speed.BaseValue * GameSettings.BaseSprintMult), (GameSettings.BaseSprintExponent)) * GameSettings.BaseSprintTime);
            Stamina = new Stat(stamina);
            lastTraumas = Traumas.CurrentValue;
            lastWounds = Wounds.CurrentValue;
        }

        public virtual void Update()
        {
            if (isDoll)
                return;

            DoMovement();
            DoMouseLook();
            GetReticleTarget();
            CheckForDamage();
            UpdateEffects();

            if (Input.GetButtonDown("Submit") && !UI.statusPanel.gameObject.activeSelf)
            {
                ShowMouse();
                UI.statusPanel.gameObject.SetActive(true);
                UI.statusPanel.InitializeMenu(this);
            }

            if (Input.GetButtonDown("Cancel") && UI.statusPanel.gameObject.activeSelf)
            {
                UI.statusPanel.gameObject.SetActive(false);
                HideMouse();
            }

            if (!IsWinded && Stamina <= 0)
                IsWinded = true;

            if (IsWinded && Stamina.Damage < 1)
                IsWinded = false;

            if (!IsSprinting)
                Stamina.RestoreStat(GameSettings.BaseStaminaRegen * Time.deltaTime);

            if (IsSprinting)
                Stamina.DamageStat(GameSettings.BaseSprintDrain * Time.deltaTime);
        }

        /// <summary>
        /// 
        /// </summary>
        private void GetReticleTarget()
        {
            //Make sure the UI has been created
            if (UI == null)
            {
                UI = Instantiate(UIPrefab);
            }

            //create a ray emitting from the center of the camera
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            
            //create a list of objects 
            List<RaycastHit> rayHits = new List<RaycastHit>(Physics.RaycastAll(ray).Where(h => 
                {
                    //restrict ray hits such that they exclude the current player, are less than the activation distancs from the current player, and have an Activator in their parent hierarchy
                    return (h.collider.gameObject != gameObject && h.distance <= GameSettings.ActivateDistance && h.collider.gameObject.GetParentActivator() != null);
                })
                //sort ray hits by distance from the player
                .OrderBy(h => h.distance));

            //choose the nearest ray hit as THE reticle object
            ReticleInfo = rayHits.FirstOrDefault();

            //make sure the chosen hit has a collider, otherwise return
            if (ReticleInfo.collider != null)
            {
                GameObject targetActivator = ReticleInfo.collider.gameObject.GetParentActivator();

                //if the targeted activator is different from the current targeted activator, update it and post a message of the new activator
                if (targetActivator != reticleObject)
                {
                    reticleObject = targetActivator;
                    UI.ReticleSprite.color = Color.green;
                    Debug.Log(targetActivator.name);
                }
            }
            else
            {
                reticleObject = null;
                UI.ReticleSprite.color = Color.white;
            }

            //activate targeted object
            if (reticleObject != null && Input.GetButtonDown("Activate") && reticleObject.GetComponent<Assets.Scripts.Activator>() != null)
                if (reticleObject.GetComponent<Assets.Scripts.Activator>() is Container)
                    //StartCoroutine(OpeningChest(reticleObject));
                    StartCoroutine(OpenChestServer(reticleObject));
                else
                {
                    string uIdenity = reticleObject.transform.name;
                    string meIdenity = gameObject.name;
                    CmdTellServerWhichDoorWasActivated(uIdenity, meIdenity);
                    //reticleObject.GetComponent<Assets.Scripts.Activator>().OnActivate(this);
                }
        }

        [Command]
        void CmdTellServerWhichDoorWasActivated(string uniqueID, string userID)
        {
            GameObject go = GameObject.Find(uniqueID);
            Player user = GameObject.Find(userID).GetComponent<Player>();
            go.GetComponent<Assets.Scripts.Activator>().OnActivate(user);
        }

        IEnumerator OpenChestServer(GameObject chest)
        {
            yield return new WaitForSeconds(GameSettings.SearchTime);

            string go = chest.transform.name;
            string user = gameObject.name;
            CmdTellServerWhichChestWasActivated(go, user);
        }

        [Command]
        void CmdTellServerWhichChestWasActivated(string uniqueID, string userID)
        {
            GameObject go = GameObject.Find(uniqueID);
            Player user = GameObject.Find(userID).GetComponent<Player>();

            if (reticleObject == go)
                reticleObject.GetComponent<Assets.Scripts.Activator>().OnActivate(user);
        }

        IEnumerator OpeningChest(GameObject chest)
        {
            yield return new WaitForSeconds(GameSettings.SearchTime);
            if (reticleObject == chest)
                reticleObject.GetComponent<Assets.Scripts.Activator>().OnActivate(this);
        }

        private void DoMovement()
        {
            //get multiplatform input
            Vector2 input = new Vector2
            {
                x = Input.GetAxis("Horizontal"),
                y = Input.GetAxis("Vertical")
            };

            //if no input is detected, reset animation vars and return
            if (input.magnitude == 0 || isInMenu)
            {
                animationController.SetInteger("animSpeed", 0);
                animationController.SetInteger("animDirection", 0);

                return;
            }

            float direction;

            //determine movement direction,
            if (input.y >= 0)
                direction = Mathf.Rad2Deg * Mathf.Atan2(input.y, input.x) - 90;
            //reverse movement direction for backpeddaling
            else
                direction = Mathf.Rad2Deg * Mathf.Atan2(-input.y, -input.x) - 90;

            //player is moving, ensure that the body is always pointing in the direction of movement
            transform.Rotate(0f, headRotate - direction, 0f, Space.World);
            headRotate = direction;

            //determine if player is sprinting
            IsSprinting = Input.GetAxis("Sprint") > 0 && !IsWinded;

            //player is standing still or moving forward
            if (input.y >= 0)
            {
                animationController.SetInteger("animDirection", 1);

                //Player is moving more forward than sideways, animate as moving forward at an angle
                if (input.y >= Mathf.Abs(input.x))
                {
                    if (IsSprinting)
                        animationController.SetInteger("animSpeed", 3);
                    else
                        animationController.SetInteger("animSpeed", (int)(input.magnitude * 1.5f));
                }
                //player is moving more sideways than forward, animate as strafing
                else
                {
                    if (IsSprinting)
                        animationController.SetInteger("animSpeed", 2);
                    else
                        animationController.SetInteger("animSpeed", (int)(input.magnitude * 1.5));
                }
            }
            //player is moving backwards
            else
            {
                animationController.SetInteger("animDirection", -1);
                animationController.SetInteger("animSpeed", 1);
            }
        }

        private void DoMouseLook()
        {
            if (isInMenu)
                return;

            //dude I don't even know

            Vector3 input = new Vector3
            {
                x = Input.GetAxis("Look X"),
                y = Input.GetAxis("Look Y"),
                z = 0
            };
            
            headRotate += input.x;
            camPivot = Mathf.Clamp(camPivot + input.y, -camClampY, camClampY);

            if (Math.Abs(headRotate) > headClampX)
            {
                float bodyX;
                if (headRotate < 0)
                {
                    bodyX = headRotate + headClampX;
                    headRotate = -headClampX;
                }
                else
                {
                    bodyX = headRotate - headClampX;
                    headRotate = headClampX;
                }
                
                transform.Rotate(0f, bodyX, 0f, Space.World);
            }

            if (Math.Abs(camPivot) > headClampY)
            {
                float camY;
                if (camPivot < 0)
                {
                    camY = camPivot + headClampY;
                    headPivot = -headClampY;
                }
                else
                {
                    camY = camPivot - headClampY;
                    headPivot = headClampY;
                }
            }
            else
            {
                headPivot = camPivot;
            }

            cam.transform.localRotation = Quaternion.identity;
            cam.transform.Rotate(-camPivot, 0f, 0f, Space.Self);
            cam.transform.Rotate(0, headRotate, 0, Space.World);

            cam.transform.eulerAngles = new Vector3(cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, 0);

            headRotateTransform.rotation = Quaternion.identity;

            headRotateTransform.Rotate(0f, headRotate, 0f, Space.Self);
            headRotateTransform.Rotate(-headPivot, 0f, 0f, Space.World);

            animationController.Play("HeadRotate", -1, headRotateTransform.rotation.eulerAngles.y / 360f);
            animationController.Play("HeadPivot", -1, -headRotateTransform.rotation.eulerAngles.x / 360f);
            animationController.Play("HeadTilt", -1, headRotateTransform.rotation.eulerAngles.z / 360f);
        }

        void CheckForDamage()
        {
            if (Wounds.CurrentValue < lastWounds)
                StartCoroutine(FadeAlpha(UI.woundFlash));

            if (Traumas.CurrentValue < lastTraumas)
                StartCoroutine(FadeAlpha(UI.traumaFlash));

            lastTraumas = Traumas.CurrentValue;
            lastWounds = Wounds.CurrentValue;
        }

        void UpdateEffects()
        {

        }

        IEnumerator FadeAlpha(Image image)
        {
            image.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.25f);
            image.gameObject.SetActive(false);
        }

        public void OnFootStep()
        {
            GetComponent<AudioSource>().Play();
        }

        public void AddItems(Dictionary<InventoryItem, int> inventory)
        {
            foreach (var kvp in inventory)
            {
                AddItem(kvp.Key, kvp.Value);
            }
        }

        public void AddItem(InventoryItem item, int count)
        {
            if (Inventory.ContainsKey(item))
                Inventory[item] += count;
            else
                Inventory.Add(item, count);

            if (item.IsArtifact)
            {
                GetComponent<gameClient>().activateAwaken();
                GetComponent<gameClient>().startedAwakening = true;
                Debug.Log("STARTED AWAKENING = " + GetComponent<gameClient>().startedAwakening);
            }
        }
    }

    public enum PlayerStat
    {
        Brawn,
        Speed,
        Intellect,
        Willpower,
        Wounds,
        Traumas
    }
}
