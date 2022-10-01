using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Armor
{
    public class ArmorSetBehaviour : MonoBehaviour
    {
        public string[] set; // dictionary names of armor
        public string[] aset; // dictionary names of attachments

        public void SpawnArmor()
        {
            if (set != null) // armor
            {
                for (int i = 0; i <= set.Length - 1; i++)
                {
                    ArmorProperties armor = Mod.armor[set[i]];

                    Sprite sprite;
                    if (armor.sprites == null)
                        sprite = armor.sprite;
                    else
                        sprite = armor.sprites[0];

                    GameObject obj = ModAPI.CreatePhysicalObject("Armor", sprite);
                    obj.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = ModAPI.FindSpawnable("Stick");
                    Destroy(obj.GetComponent<Optout>()); // this is so it can be saved

                    obj.transform.position = transform.position;

                    PhysicalBehaviour phys = GetComponent<PhysicalBehaviour>();
                    PhysicalBehaviour objPhys = obj.GetComponent<PhysicalBehaviour>();

                    objPhys.InitialMass = phys.InitialMass; // weight stuff i think
                    objPhys.TrueInitialMass = phys.TrueInitialMass;
                    objPhys.InitialGravityScale = phys.InitialGravityScale;
                    objPhys.rigidbody.mass = phys.rigidbody.mass;

                    objPhys.OverrideShotSounds = phys.OverrideShotSounds; // i dont know if this is needed anymore

                    ArmorBehaviour armorBehaviour = obj.AddComponent<ArmorBehaviour>();
                    armorBehaviour.propName = set[i];
                }
            }
            if (aset != null) // attachments
            {
                for (int i = 0; i <= aset.Length - 1; i++)
                {
                    AttachmentProperties attach = Mod.attachment[aset[i]];

                    Sprite sprite;
                    if (attach.sprites == null)
                        sprite = attach.sprite;
                    else
                        sprite = attach.sprites[0];

                    GameObject obj = ModAPI.CreatePhysicalObject("Armor", sprite);
                    obj.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = ModAPI.FindSpawnable("Stick");
                    Destroy(obj.GetComponent<Optout>());

                    obj.transform.position = transform.position;

                    PhysicalBehaviour phys = GetComponent<PhysicalBehaviour>();
                    PhysicalBehaviour objPhys = obj.GetComponent<PhysicalBehaviour>();

                    objPhys.InitialMass = phys.InitialMass;
                    objPhys.TrueInitialMass = phys.TrueInitialMass;
                    objPhys.InitialGravityScale = phys.InitialGravityScale;
                    objPhys.rigidbody.mass = phys.rigidbody.mass;

                    objPhys.OverrideShotSounds = phys.OverrideShotSounds;

                    AttachmentBehaviour attachmentBehaviour = obj.AddComponent<AttachmentBehaviour>();
                    attachmentBehaviour.propName = aset[i];
                }
            }
        }
    }
    // makes armor get damaged and stuff
    public class Armor : MonoBehaviour
    {
        [SerializeField]
        public bool start = true;

        [SerializeField]
        public string propName;

        public PhysicalBehaviour phys;
        public PhysicalProperties physicalProperties; // convenience

        [SerializeField]
        public float armorPoints;
        public float initialPoints;
        [SerializeField]
        public float customInitialPoints; // initial points set to this value when it is not zero
        public bool destroyed; // this is set at start through updatedamage
        [SerializeField]
        public bool durabilityDisabled;

        public float initialAbsorb;
        public float initialBrittle;

        [SerializeField]
        public bool modified; // sets initialabsorb to modifiedabsorption when true
        [SerializeField]
        public float modifiedAbsorption;

        public bool disableCollisionOnDestroyed = true; // this is here cause im tired

        public virtual void OnFragmentHit(float force)
        {
            if (durabilityDisabled)
                return;
            armorPoints -= force;
            UpdateDamage();
        }
        public void UpdateDamage()
        {
            armorPoints = Mathf.Max(armorPoints, 0);
            if (armorPoints == 0)
            {
                if (disableCollisionOnDestroyed)
                    gameObject.layer = 10; // disabled collision layer
                destroyed = true;
            }
            else
            {
                if (disableCollisionOnDestroyed)
                    gameObject.layer = 9;
                destroyed = false;
            }
            float percent = armorPoints / initialPoints;
            physicalProperties.BulletSpeedAbsorptionPower = Mathf.SmoothStep(0, initialAbsorb, percent);
            physicalProperties.Brittleness = Mathf.SmoothStep(1, initialBrittle, percent);
        }
        void Shot(Shot shot)
        {
            if (durabilityDisabled)
                return;
            // damage - 8 small, 15 medium, 25 high, 50 very high, 100 massive
            // speed - 6 low, 10 medium, 15 high, 25 very high, 35 massive
            // result - 4.8 low, 15 medium, 37.5 high, 62.5 very high (minigun), 350 massive
            float damage = Mathf.Min(shot.damage, shot.cartridge.Damage) * shot.cartridge.StartSpeed; // magnifying the difference between low caliber and high caliber weapons
            armorPoints -= damage / 10;
            UpdateDamage();
        }
        public void Nocollide(GameObject col)
        {
            NoCollide noCol = gameObject.AddComponent<NoCollide>();
            noCol.NoCollideSetA = GetComponents<Collider2D>();
            noCol.NoCollideSetB = col.GetComponents<Collider2D>();
        }
        public virtual void ContextMenu()
        {
            List<ContextMenuButton> buttons = phys.ContextMenuOptions.Buttons;
            ContextMenuButton[] contextMenuButtonArray = new ContextMenuButton[4];
            ContextMenuButton contextMenuButton = new ContextMenuButton("checkQualityFA", "Check armor quality", "Check how effective the armor is.", new UnityAction[1]
            {
                () => 
                {
                    if (armorPoints != 0)
                    {
                        int effectiveness = (int)((Mathf.SmoothStep(0, 1, armorPoints / initialPoints) * 100) + 0.5f);
                        ModAPI.Notify("Armor points " + Mathf.Round(armorPoints * 10) / 10 + " / " + initialPoints + " -- " + effectiveness + "% effectiveness");
                    }
                    else
                        ModAPI.Notify("<color=red>Armor points 0 / " + initialPoints + " -- 0% effectiveness</color>");
                }
            });
            contextMenuButton.LabelWhenMultipleAreSelected = "Check armor quality";
            contextMenuButtonArray[0] = contextMenuButton;

            contextMenuButton = new ContextMenuButton("repairArmorFA", "Repair armor", "Repairs the armor.", new UnityAction[1]
            {
                () =>
                {
                    armorPoints = initialPoints;
                    UpdateDamage();
                    ModAPI.Notify("Armor repaired!");
                }
            });
            contextMenuButton.LabelWhenMultipleAreSelected = "Repair armor";
            contextMenuButtonArray[1] = contextMenuButton;

            contextMenuButton = new ContextMenuButton("modifyDurabilityFA", "Modify durability", "Modify or disable the durability of the armor.", new UnityAction[1]
            {
                () =>
                {
                    DialogBox dialog = null;
                    dialog = DialogBoxManager.TextEntry("Input a value for max durability. This will repair the armor.", "", new DialogButton("Apply", true, new UnityAction[1]
                    {
                        () =>
                        {
                            if (dialog.EnteredText != "" && float.TryParse(dialog.EnteredText, out float result))
                            {
                                customInitialPoints = result;
                                initialPoints = result;
                                armorPoints = initialPoints;
                                UpdateDamage();
                            }
                            else if (dialog.EnteredText == "")
                                ModAPI.Notify("You didn't input anything.");
                            else
                                ModAPI.Notify("You didn't input a number.");
                        }
                    }),
                    new DialogButton(durabilityDisabled ? "Enable durability" : "Disable durability", true, new UnityAction[1]
                    {
                        () =>
                        {
                            if (durabilityDisabled)
                                durabilityDisabled = false;
                            else
                            {
                                armorPoints = initialPoints;
                                UpdateDamage();
                                durabilityDisabled = true;
                            }
                        }
                    }),
                    new DialogButton("Cancel", true, new UnityAction[1]
                    {
                        () => dialog.Close()
                    }));
                }
            });
            contextMenuButton.LabelWhenMultipleAreSelected = "Modify durability";
            contextMenuButtonArray[2] = contextMenuButton;

            contextMenuButton = new ContextMenuButton("modifyHardnessFA", "Modify hardness", "Modify how well the armor can stop bullets.", new UnityAction[1]
            {
                () =>
                {
                    DialogBox dialog = null;
                    dialog = DialogBoxManager.TextEntry("Input a value for absorption. Current absorption is " + (modified ? modifiedAbsorption : physicalProperties.BulletSpeedAbsorptionPower) + ".", "", new DialogButton("Apply", true, new UnityAction[1]
                    {
                        () =>
                        {
                            if (dialog.EnteredText != "" && float.TryParse(dialog.EnteredText, out float result))
                            {
                                modifiedAbsorption = result;
                                initialAbsorb = result;
                                modified = true;
                                UpdateDamage();
                            }
                            else if (dialog.EnteredText == "")
                                ModAPI.Notify("You didn't input anything.");
                            else
                                ModAPI.Notify("You didn't input a number.");
                        }
                    }),
                    new DialogButton("Cancel", true, new UnityAction[1]
                    {
                        () => dialog.Close()
                    }));
                }
            });
            contextMenuButton.LabelWhenMultipleAreSelected = "Modify hardness";
            contextMenuButtonArray[3] = contextMenuButton;
            buttons.AddRange(contextMenuButtonArray);
        }
    }
    // makes armor attach to limbs
    public class ArmorBehaviour : Armor, Messages.IUse
    {
        bool attached;
        [SerializeField]
        public LimbBehaviour attachedLimb;
        HingeJoint2D joint;
        public List<AttachmentBehaviour> attachments = new List<AttachmentBehaviour>(); // used to snap all attachments when armor attaches to something or activate all attachments
                                                                                       
        private ArmorProperties prop; // convenience

        [SerializeField]
        public int spriteIndex = -1; // for armor with multiple sprites; stops sprite from changing when saved

        void Start()
        {
            phys = GetComponent<PhysicalBehaviour>();
            prop = Mod.armor[propName];

            SetProperties();
            ContextMenu();
            phys.RefreshOutline();

            if (attachedLimb)
            {
                Nocollide(attachedLimb.gameObject);
                Attach(attachedLimb);
            }

            phys.HoldingPositions = new Vector3[0];
            UpdateDamage();
        }
        void Update()
        {
            if (attachedLimb)
                IgnoreGrabbedObjects();
            else if (attached) // armor is deleted if whatever it is attached to is also deleted
                Destroy(gameObject);
        }
        public void Use(ActivationPropagation activation)
        {
            if (attachments.Count > 0)
            {
                foreach (AttachmentBehaviour attach in attachments)
                {
                    attach.Activate();
                }
            }
        }
        void SetProperties()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (prop.sprites == null)
                sr.sprite = prop.sprite; // uses regular sprite if the armor does not have multiple sprites
            else if (spriteIndex == -1)
            {
                int random = Random.Range(0, prop.sprites.Length);
                sr.sprite = prop.sprites[random];
                spriteIndex = random; // randomly chooses sprite, then saves the index for saving
            }
            else
                sr.sprite = prop.sprites[spriteIndex]; // sets sprite to spriteindex if spriteindex has already been set

            if (customInitialPoints == 0)
                initialPoints = prop.initialPoints;
            else
                initialPoints = customInitialPoints;

            physicalProperties = Instantiate(prop.physicalProperties); // instantiated so properties aren't changed for all armor

            if (modified)
                initialAbsorb = modifiedAbsorption;
            else
                initialAbsorb = physicalProperties.BulletSpeedAbsorptionPower;
            initialBrittle = physicalProperties.Brittleness;

            if (start) // lets damaged armor stay damaged when saving
            {
                armorPoints = initialPoints;
                start = false;
            }

            phys.Properties = physicalProperties;

            if (prop.colliderPoints == null)
                gameObject.FixColliders();
            else
            {
                foreach (Collider2D collider in GetComponents<Collider2D>())
                    Destroy(collider);
                gameObject.AddComponent<PolygonCollider2D>().points = prop.colliderPoints;
            }
            disableCollisionOnDestroyed = prop.disableCollisionWhenDestroyed;
        }
        public override void OnFragmentHit(float force)
        {
            if (attachedLimb && (destroyed || armorPoints - (force * 5) < 0))
            {
                attachedLimb.SendMessage("OnFragmentHit", force, SendMessageOptions.DontRequireReceiver);
            }
            base.OnFragmentHit(force);
        }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent(out LimbBehaviour limb) || collision.gameObject.GetComponent<Armor>())
            {
                Nocollide(collision.gameObject);
                if (!attached && limb && limb.gameObject.name == prop.armorPiece)
                    Attach(limb);
            }
            else if (collision.gameObject.TryGetComponent(out PhysicalBehaviour phys))
            {
                if (phys.beingHeldByGripper)
                {
                    foreach (LimbBehaviour l in attachedLimb.Person.Limbs)
                    {
                        if (l == null || l.GripBehaviour == null)
                            continue;
                        if (l.GripBehaviour.CurrentlyHolding == phys)
                            Nocollide(l.GripBehaviour.CurrentlyHolding.gameObject);
                    }
                }
            }

        }
        void Attach(LimbBehaviour limb)
        {
            GetComponent<Rigidbody2D>().angularVelocity = 0;
            SpriteRenderer sr = GetComponent<SpriteRenderer>();

            if (prop.setSortingOrder == true)
                sr.sortingOrder = prop.sortingOrder;
            else
                sr.sortingOrder = limb.gameObject.GetComponent<SpriteRenderer>().sortingOrder + prop.sortingOrder;

            if (prop.layer != "")
                sr.sortingLayerName = prop.layer;
            else
                sr.sortingLayerName = limb.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;

            attached = true;
            GetComponent<Rigidbody2D>().isKinematic = true;
            transform.parent = limb.transform;
            transform.localEulerAngles = new Vector3(0, 0, 0);
            transform.localPosition = prop.offset;
            transform.localScale = Vector3.one + prop.scaleOffset;
            transform.parent = null;

            /*
            FixedJoint2D joint = gameObject.AddComponent<FixedJoint2D>();
            joint.dampingRatio = 1;
            joint.frequency = 0;
            joint.connectedBody = limb.GetComponent<Rigidbody2D>();
            */

            joint = limb.gameObject.AddComponent<HingeJoint2D>();
            joint.connectedBody = GetComponent<Rigidbody2D>();
            joint.useLimits = true;
            JointAngleLimits2D limits = new JointAngleLimits2D() { max = 0, min = 0 };
            joint.limits = limits;

            GetComponent<Rigidbody2D>().isKinematic = false;
            attachedLimb = limb;

            limb.gameObject.SendMessage("CopyPosition", gameObject, SendMessageOptions.DontRequireReceiver);
            if (attachments.Count > 0)
            {
                foreach (AttachmentBehaviour attach in attachments)
                {
                    attach.Snap(gameObject);
                }
            }
        }
        public void IgnoreGrabbedObjects()
        {
            foreach (LimbBehaviour limb in attachedLimb.Person.Limbs)
            {
                if (limb || limb.GripBehaviour == null)
                    continue;
                if (limb.GripBehaviour.CurrentlyHolding.gameObject != null)
                    Nocollide(limb.GripBehaviour.CurrentlyHolding.gameObject);
            }
        }
        void OnDestroy()
        {
            foreach (AttachmentBehaviour attachment in attachments)
            {
                Destroy(attachment.gameObject);
            }
        }
        public override void ContextMenu()
        {
            base.ContextMenu();
            if (prop.sprites != null)
            {
                GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("nexttexturebut", "Next texture", "Changes the texture.", new UnityAction[1]
                {
                    () =>
                    {
                        SpriteRenderer sr = GetComponent<SpriteRenderer>();
                        if (spriteIndex >= prop.sprites.Length - 1)
                            spriteIndex = 0;
                        else
                            spriteIndex += 1;
                        sr.sprite = prop.sprites[spriteIndex];
                        phys.RefreshOutline();
                    }
                })
                { LabelWhenMultipleAreSelected = "Next texture" });
                GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("backtexturebut", "Previous texture", "Changes the texture.", new UnityAction[1]
                {
                    () =>
                    {
                        SpriteRenderer sr = GetComponent<SpriteRenderer>();
                        if (spriteIndex <= 0)
                            spriteIndex = prop.sprites.Length - 1;
                        else
                            spriteIndex -= 1;
                        sr.sprite = prop.sprites[spriteIndex];
                        phys.RefreshOutline();
                    }
                })
                { LabelWhenMultipleAreSelected = "Previous texture"});
            }
            GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("detachbut", "Detach", "Detaches the armor from whatever body part it is on.", new UnityAction[1]
            {
            () =>
                {
                    if (attachedLimb)
                    {
                        Destroy(joint);
                        attached = false;
                        attachedLimb = null;
                    }
                }
            })
            { LabelWhenMultipleAreSelected = "Detach"});
        }
    }
    // has the properties of armor like its health and sprite
    // used when creating armor
    public class ArmorProperties
    {
        public ArmorProperties(string armorPiece, Sprite sprite, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            offset = Vector3.zero;
            scaleOffset = Vector3.zero;
            this.physicalProperties = physicalProperties;
            this.sprite = sprite;
            sprites = null;
            colliderPoints = null;
        }
        public ArmorProperties(string armorPiece, Sprite sprite, Vector3 offset, Vector3 scaleOffset, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            this.offset = offset;
            this.scaleOffset = scaleOffset;
            this.physicalProperties = physicalProperties;
            this.sprite = sprite;
            sprites = null;
            colliderPoints = null;
        }
        public ArmorProperties(string armorPiece, Sprite sprite, Vector3 offset, Vector3 scaleOffset, Vector2[] colliderPoints, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            this.offset = offset;
            this.scaleOffset = scaleOffset;
            this.physicalProperties = physicalProperties;
            this.sprite = sprite;
            sprites = null;
            this.colliderPoints = colliderPoints;
        }
        public ArmorProperties(string armorPiece, Sprite[] sprites, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            offset = Vector3.zero;
            scaleOffset = Vector3.zero;
            this.physicalProperties = physicalProperties;
            sprite = null;
            this.sprites = sprites;
            colliderPoints = null;
        }
        public ArmorProperties(string armorPiece, Sprite[] sprites, Vector3 offset, Vector3 scaleOffset, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            this.offset = offset;
            this.scaleOffset = scaleOffset;
            this.physicalProperties = physicalProperties;
            sprite = null;
            this.sprites = sprites;
            colliderPoints = null;
        }
        public ArmorProperties(string armorPiece, Sprite[] sprites, Vector3 offset, Vector3 scaleOffset, Vector2[] colliderPoints, float armorPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            initialPoints = armorPoints;
            this.offset = offset;
            this.scaleOffset = scaleOffset;
            this.physicalProperties = physicalProperties;
            sprite = null;
            this.sprites = sprites;
            this.colliderPoints = colliderPoints;
        }

        [SkipSerialisation]
        public float initialPoints;
        [SkipSerialisation]
        public string armorPiece; // name of what it attaches to

        [SkipSerialisation]
        public PhysicalProperties physicalProperties;

        [SkipSerialisation]
        public Vector2[] colliderPoints; // custom collider
        [SkipSerialisation]
        public Vector3 offset;
        [SkipSerialisation]
        public Vector3 scaleOffset;

        public Sprite sprite; // single sprite
        public Sprite[] sprites; // multiple sprites

        public string layer = "";
        public bool setSortingOrder = false;
        public int sortingOrder = 1;

        public bool disableCollisionWhenDestroyed = true; // this is for if you've made a custom collider with a void in the center of the armor. it is not required ever
    }
}
