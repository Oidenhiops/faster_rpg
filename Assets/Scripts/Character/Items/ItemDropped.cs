using UnityEngine;

public class ItemDropped : InteractableBase
{
    public float amplitude = 0.05f;
    public float frequency = 3f;
    private Vector3 startLocalPos;
    public Rigidbody rb;
    public Transform bounceTransform;
    public CharacterData.CharacterItem itemData;
    public MeshRenderer itemMeshRenderer;
    public BoxCollider colliderForPickUp;
    public bool _canBePickedUp = false;
    public bool canBePickedUp
    {
        get => _canBePickedUp;
        set
        {
            _canBePickedUp = value;
            if (value)
            {
                colliderForPickUp.enabled = true;
            }
            else
            {
                colliderForPickUp.enabled = false;
            }
        }
    }
    public Mesh originalMesh;
    void Start()
    {
        startLocalPos = bounceTransform.localPosition;
    }
    void Update()
    {
        bounceTransform.localPosition = startLocalPos + Vector3.up * Mathf.Sin(Time.time * frequency) * amplitude;
    }
    public void InitializeDropItem(CharacterData.CharacterItem itemData, bool startCountToPickUp = false)
    {
        this.itemData = itemData;
        itemMeshRenderer.material.SetTexture("_MainTex", itemData.itemBaseSO.icon.texture);
        SetTextureFromAtlas(itemData.itemBaseSO.icon, itemMeshRenderer);
        if (this.itemData.itemBaseSO is ActivableItemSO activableItemSO && activableItemSO.activableItemPrefab)
        {
            Instantiate(activableItemSO.activableItemPrefab, transform.position + Vector3.up, Quaternion.identity, transform);
        }
        if (startCountToPickUp) _ = StartCountToPickUp();
    }
    private async Awaitable StartCountToPickUp()
    {
        await Awaitable.WaitForSecondsAsync(1f);
        canBePickedUp = true;
    }
    public override void Interact(CharacterBase characterBase)
    {
        characterBase.OnHandlePickUpItem(this);
    }
    public override Sprite GetInteractIcon()
    {
        return itemData.itemBaseSO.icon;
    }
    public override string GetInteractText()
    {
        return GameData.Instance.GetDialog(itemData.itemBaseSO.idText, GameData.TypeLOCS.Items).dialog;
    }
    void SetTextureFromAtlas(Sprite spriteFromAtlas, MeshRenderer meshRenderer)
    {
        Vector2[] uvs = originalMesh.uv;
        Texture2D texture = spriteFromAtlas.texture;
        meshRenderer.material.mainTexture = texture;
        Rect spriteRect = spriteFromAtlas.rect;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].x = Mathf.Lerp(spriteRect.x / texture.width, (spriteRect.x + spriteRect.width) / texture.width, uvs[i].x);
            uvs[i].y = Mathf.Lerp(spriteRect.y / texture.height, (spriteRect.y + spriteRect.height) / texture.height, uvs[i].y);
        }
        meshRenderer.GetComponent<MeshFilter>().mesh.uv = uvs;
    }
    void OnTriggerEnter(Collider other)
    {
        if (canBePickedUp && other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out CharacterPlayer characterPlayer))
            {
                if (!characterPlayer.interactables.ContainsKey(this))
                {
                    characterPlayer.interactables.Add(this, gameObject);
                    characterPlayer.OnShowItemsToPickUp?.Invoke();
                }
            }
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (canBePickedUp && other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out CharacterPlayer characterPlayer))
            {
                if (characterPlayer.interactables.ContainsKey(this))
                {
                    characterPlayer.interactables.Remove(this);
                    characterPlayer.OnShowItemsToPickUp?.Invoke();
                }
            }
        }
    }
}
