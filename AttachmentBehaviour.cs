using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace Armor
{
    public class AttachmentBehaviour : Armor, Messages.IUse
    {
        bool attached;
        [SerializeField]
        public ArmorBehaviour attachedArmor;

        GameObject sprite; // sprite object, only for animation so the actual attachment doesn't have to be rotated around
        GameObject pivot; // pivot for the sprite object

        Collider2D[] results = new Collider2D[4]; // collider check when activated. isn't resized

        bool active = true; // attachments spawn activated

        AttachmentProperties prop;
        Coroutine anim; // for stopping animation coroutines early
        FixedJoint2D connection; // connection to attached armor piece, destroyed when snapping or detaching
        // unsure if this can cause issues with detaching or snapping after saving

        [SerializeField]
        public int spriteIndex = -1; // for multiple sprites

        void Start()
        {
            phys = GetComponent<PhysicalBehaviour>();
            prop = Mod.attachment[propName];

            SetProperties();
            ContextMenu();
            phys.RefreshOutline();

            phys.HoldingPositions = new Vector3[0];

            if (prop.type != AttachmentProperties.ActivationType.None)
            {
                // creates a sprite for animation and a pivot for that sprite
                pivot = new GameObject();
                pivot.transform.parent = transform;
                pivot.transform.localPosition = prop.pivot;
                pivot.transform.localScale = Vector3.one;
                pivot.transform.localRotation = Quaternion.Euler(0, 0, prop.activatedAngle);
                pivot.AddComponent<Optout>();
                sprite = new GameObject("sprite");
                sprite.transform.parent = transform;
                sprite.transform.localPosition = Vector3.zero;
                sprite.transform.localScale = Vector3.one;
                sprite.transform.localRotation = Quaternion.Euler(0, 0, prop.activatedAngle);
                sprite.transform.parent = pivot.transform;
                SpriteRenderer sr = sprite.AddComponent<SpriteRenderer>();
                sprite.AddComponent<Optout>();
                if (prop.sprites == null)
                    sr.sprite = prop.sprite;
                else
                    sr.sprite = prop.sprites[spriteIndex];
                sr.enabled = false;
            }

            if (attachedArmor)
                Attach(attachedArmor); // must be after creating sprite and pivot objects

            UpdateDamage();
        }
        void Update()
        {
            if (attached && !attachedArmor)
                Destroy(gameObject);
        }
        void SetProperties()
        { 
            SpriteRenderer sr = GetComponent<SpriteRenderer>();

            if (prop.sprites == null)
                sr.sprite = prop.sprite;
            else if (spriteIndex == -1)
            {
                int random = Random.Range(0, prop.sprites.Length);
                sr.sprite = prop.sprites[random];
                spriteIndex = random;
            }
            else
                sr.sprite = prop.sprites[spriteIndex];

            if (customInitialPoints == 0)
                initialPoints = prop.initialPoints;
            else
                initialPoints = customInitialPoints;

            physicalProperties = Instantiate(prop.physicalProperties);

            if (modified)
                initialAbsorb = modifiedAbsorption;
            else
                initialAbsorb = physicalProperties.BulletSpeedAbsorptionPower;
            initialBrittle = physicalProperties.Brittleness;

            if (start)
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
                PolygonCollider2D poly = gameObject.AddComponent<PolygonCollider2D>();
                poly.points = prop.colliderPoints;
            }
        }
        public void Use(ActivationPropagation activation)
        {
            if (!attached)
            {
                ContactFilter2D filter = new ContactFilter2D();
                filter.NoFilter();
                Physics2D.OverlapBox(transform.position, transform.localScale, transform.rotation.z, filter, results); // checks for overlapping colliders
                if (results != null)
                {
                    foreach (Collider2D col in results)
                    {
                        if (col && col.TryGetComponent(out ArmorBehaviour armor) && armor.propName == prop.armorPiece)
                        {
                            Attach(armor);
                            break;
                        }
                    }
                }
            }
            else
                Activate();
        }
        public void Activate()
        {
            if (!attached || prop.type != AttachmentProperties.ActivationType.Use)
                return;
            if (active)
            {
                active = false;
                if (anim != null)
                    StopCoroutine(anim); // stops previous animation just in case it is still going
                anim = StartCoroutine(animateRotation(prop.activatedAngle, prop.deactivatedAngle, true));
                gameObject.layer = 10; // disables collision
            }
            else
            {
                active = true;
                if (anim != null)
                    StopCoroutine(anim);
                anim = StartCoroutine(animateRotation(prop.deactivatedAngle, prop.activatedAngle, false));
                gameObject.layer = 9;
            }
        }
        void OnCollisionEnter2D(Collision2D col)
        {
            if (col.gameObject.GetComponent<Armor>() || col.gameObject.GetComponent<LimbBehaviour>())
            {
                Nocollide(col.gameObject);
            }
        }
        public IEnumerator animateRotation(float start, float angle, bool visible)
        {
            if (!active)
                GetComponent<SpriteRenderer>().enabled = false;
            pivot.transform.localRotation = Quaternion.Euler(0, 0, start);
            sprite.GetComponent<SpriteRenderer>().enabled = true;
            for (float i = 0; i < 1; i += Time.fixedDeltaTime / 1.5f) // animation takes 1.5 seconds
            {
                pivot.transform.localRotation = Quaternion.Slerp(pivot.transform.localRotation, Quaternion.Euler(0, 0, angle), i);
                yield return new WaitForFixedUpdate();
            }
            pivot.transform.localRotation = Quaternion.Euler(0, 0, angle); // changes rotation just in case
            sprite.GetComponent<SpriteRenderer>().enabled = visible;
            if (active)
                GetComponent<SpriteRenderer>().enabled = true;
        }
        void Attach(ArmorBehaviour armor)
        {
            Snap(armor.gameObject);

            attachedArmor = armor;
            armor.attachments.Add(this);
        }
        public void Snap(GameObject target)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.angularVelocity = 0;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            sr.sortingOrder = target.GetComponent<SpriteRenderer>().sortingOrder + 2;
            sr.sortingLayerName = target.GetComponent<SpriteRenderer>().sortingLayerName; // keeps the attachment on top of the armor
            if (sprite)
            {
                SpriteRenderer sr2 = sprite.GetComponent<SpriteRenderer>();
                sr2.sortingOrder = sr.sortingOrder + 1;
                sr2.sortingLayerName = sr.sortingLayerName;
            }

            attached = true;
            rb.isKinematic = true;
            transform.parent = target.transform;
            transform.localEulerAngles = new Vector3(0, 0, 0);
            transform.localPosition = prop.attachmentPoint;
            transform.localScale = Vector3.one + prop.scaleOffset;
            transform.parent = null;

            if (connection)
                Destroy(connection); // destroys the previous connection, so that there is only ever one
            connection = gameObject.AddComponent<FixedJoint2D>();
            connection.dampingRatio = 1;
            connection.frequency = 0;
            connection.connectedBody = target.GetComponent<Rigidbody2D>();

            rb.isKinematic = false;
            active = true;
        }
        public void Detach()
        {
            if (attached)
            {
                if (!active)
                {
                    ModAPI.Notify("The attachment must be activated in order to detach it."); // prevents bugs, im lazy
                    return;
                }
                attachedArmor.attachments.Remove(this);
                attachedArmor.attachments.Capacity -= 1; // resizing because remove doesnt do that apparently
                Destroy(connection);
                attached = false;
                attachedArmor = null;
            }
        }
        public override void ContextMenu()
        {
            base.ContextMenu();
            GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("alignbut", "Realign", "Realigns the attachment.", new UnityAction[1]
            {
                () =>
                {
                    if (attached)
                        Snap(attachedArmor.gameObject);
                    else
                        ModAPI.Notify("The attachment isn't attached to anything.");
                }
            }));
            GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("detachbut", "Detach", "Detaches the attachment from whatever it is attached to.", new UnityAction[1]
            {
                () =>
                {
                    Detach();
                }
            }));
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
                }));
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
                }));
            }
        }
    }

    public class AttachmentProperties
    {
        public AttachmentProperties(string armorPiece, Sprite sprite, Vector2 attachmentPoint, Vector3 scaleOffset, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            this.sprite = sprite;
            sprites = null;
            activatedAngle = 0;
            deactivatedAngle = 0;
            pivot = Vector2.zero;
            this.attachmentPoint = attachmentPoint;
            colliderPoints = null;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            type = ActivationType.None;
        }
        public AttachmentProperties(string armorPiece, Sprite sprite, Vector2 attachmentPoint, Vector3 scaleOffset, Vector2[] colliderPoints, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            this.sprite = sprite;
            sprites = null;
            activatedAngle = 0;
            deactivatedAngle = 0;
            pivot = Vector2.zero;
            this.attachmentPoint = attachmentPoint;
            this.colliderPoints = colliderPoints;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            type = ActivationType.None;
        }
        public AttachmentProperties(string armorPiece, Sprite sprite, ActivationType type, float activatedAngle, float deactivatedAngle, Vector2 pivot, Vector2 attachmentPoint, Vector3 scaleOffset, Vector2[] colliderPoints, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            this.sprite = sprite;
            sprites = null;
            this.activatedAngle = activatedAngle;
            this.deactivatedAngle = deactivatedAngle;
            this.pivot = pivot;
            this.attachmentPoint = attachmentPoint;
            this.colliderPoints = colliderPoints;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            this.type = type;
        }
        public AttachmentProperties(string armorPiece, Sprite[] sprites, Vector2 attachmentPoint, Vector3 scaleOffset, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            sprite = null;
            this.sprites = sprites;
            activatedAngle = 0;
            deactivatedAngle = 0;
            pivot = Vector2.zero;
            this.attachmentPoint = attachmentPoint;
            colliderPoints = null;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            type = ActivationType.None;
        }
        public AttachmentProperties(string armorPiece, Sprite[] sprites, Vector2 attachmentPoint, Vector3 scaleOffset, Vector2[] colliderPoints, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            sprite = null;
            this.sprites = sprites;
            activatedAngle = 0;
            deactivatedAngle = 0;
            pivot = Vector2.zero;
            this.attachmentPoint = attachmentPoint;
            this.colliderPoints = colliderPoints;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            type = ActivationType.None;
        }
        public AttachmentProperties(string armorPiece, Sprite[] sprites, ActivationType type, float activatedAngle, float deactivatedAngle, Vector2 pivot, Vector2 attachmentPoint, Vector3 scaleOffset, Vector2[] colliderPoints, float initialPoints, PhysicalProperties physicalProperties)
        {
            this.armorPiece = armorPiece;
            sprite = null;
            this.sprites = sprites;
            this.activatedAngle = activatedAngle;
            this.deactivatedAngle = deactivatedAngle;
            this.pivot = pivot;
            this.attachmentPoint = attachmentPoint;
            this.colliderPoints = colliderPoints;
            this.initialPoints = initialPoints;
            this.physicalProperties = physicalProperties;
            this.scaleOffset = scaleOffset;
            this.type = type;
        }

        [SkipSerialisation]
        public ActivationType type;
        [SkipSerialisation]
        public float activatedAngle;
        [SkipSerialisation]
        public float deactivatedAngle;

        public Vector2 pivot;
        [SkipSerialisation]
        public string armorPiece; // dictionary name of the armor it attaches to
        [SkipSerialisation]
        public Vector2 attachmentPoint;
        [SkipSerialisation]
        public Vector3 scaleOffset;

        [SkipSerialisation]
        public Vector2[] colliderPoints;
        public Sprite sprite;
        public Sprite[] sprites;

        [SkipSerialisation]
        public float initialPoints;
        [SkipSerialisation]
        public PhysicalProperties physicalProperties;


        public enum ActivationType { None, Use }
    }
}
