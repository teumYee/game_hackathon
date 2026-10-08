using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A small, self-contained three-lane survival game.</summary>
[ExecuteAlways]
public class AgileDayGame : MonoBehaviour
{
    private enum GameState { Ready, Instructions, Playing, Won, Lost }
    private enum ItemKind { Bomb, Meeting, Coffee, Contest, Club }

    private sealed class FallingItem
    {
        public GameObject root;
        public ItemKind kind;
        public int lane;
        public float speed;
    }

    private readonly List<FallingItem> items = new List<FallingItem>();
    private readonly float[] laneX = { -2.2f, 0f, 2.2f };
    private GameState state = GameState.Ready;
    private GameObject player;
    private Sprite squareSprite;
    private int lane = 1;
    private int hp = 3;
    private int score;
    private float timeLeft = 45f;
    private float spawnTimer;
    private float shieldHits;
    private float reverseTimer;
    private float hitCooldown;
    private float obstacleSpeedBoost = 1f;
    private Camera gameCamera;
    private Font koreanFont;
    private SpriteRenderer playerRenderer;
    private Color playerContactColor = Color.white;
    private float playerColorTimer;
    private Texture2D opaquePanelTexture;
    private Texture2D buttonTexture;
    private readonly Dictionary<ItemKind, Sprite> itemSprites = new Dictionary<ItemKind, Sprite>();
    private Sprite playerSprite;
    [SerializeField] private string gameTitle = "민첩한 하루 되세요";
    [SerializeField] private TextMesh titlePreview;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            EnsureSceneArtwork();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            EnsureSceneArtwork();
            return;
        }

        EnsureSceneArtwork();
        if (titlePreview != null)
            titlePreview.gameObject.SetActive(false);

        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
        }

        gameCamera.orthographic = true;
        // A tighter camera view makes the playfield fill more of the Game window.
        gameCamera.orthographicSize = 2.8f;
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        gameCamera.backgroundColor = new Color(0.035f, 0.055f, 0.11f);
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        FitBackgroundToCamera(gameCamera);

        koreanFont = Resources.Load<Font>("Fonts/NotoSansKR-VF");
        Sprite[] playerSprites = Resources.LoadAll<Sprite>("player");
        if (playerSprites.Length > 0)
            playerSprite = playerSprites[0];
        LoadItemSprite(ItemKind.Bomb, "bomb");
        LoadItemSprite(ItemKind.Meeting, "alarm");
        LoadItemSprite(ItemKind.Coffee, "coffee");
        LoadItemSprite(ItemKind.Contest, "award");
        LoadItemSprite(ItemKind.Club, "letter");
        Texture2D texture = Texture2D.whiteTexture;
        // The built-in white texture is only a few pixels wide. Matching its
        // pixels-per-unit to its width gives our lane rectangles a 1x1 world size.
        squareSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width);
        BuildPlayfield();
        CreatePlayer();
    }

    private void EnsureSceneArtwork()
    {
        if (titlePreview == null)
        {
            Transform existingTitle = transform.Find("Scene Title - edit text here");
            if (existingTitle != null)
                titlePreview = existingTitle.GetComponent<TextMesh>();
            else
            {
                GameObject titleObject = new GameObject("Scene Title - edit text here");
                titleObject.transform.SetParent(transform, true);
                titlePreview = titleObject.AddComponent<TextMesh>();
                titlePreview.text = gameTitle;
                titlePreview.anchor = TextAnchor.MiddleCenter;
                titlePreview.alignment = TextAlignment.Center;
                titlePreview.fontSize = 100;
                titlePreview.characterSize = .22f;
                titlePreview.fontStyle = FontStyle.Bold;
                titlePreview.color = Color.white;
                Font font = Resources.Load<Font>("Fonts/NotoSansKR-VF");
                if (font != null) titlePreview.font = font;
                titleObject.transform.position = new Vector3(0f, 1.75f, -.5f);
                titleObject.GetComponent<MeshRenderer>().sortingOrder = 20;
                MarkSceneArtworkDirty(titleObject);
            }
        }
        if (titlePreview != null && titlePreview.font == null)
        {
            Font font = Resources.Load<Font>("Fonts/NotoSansKR-VF");
            if (font != null) titlePreview.font = font;
        }

        if (backgroundRenderer == null)
        {
            Transform existingBackground = transform.Find("Campus Background");
            if (existingBackground != null)
                backgroundRenderer = existingBackground.GetComponent<SpriteRenderer>();
            else
            {
                GameObject backgroundObject = new GameObject("Campus Background");
                backgroundObject.transform.SetParent(transform, true);
                backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
                Sprite[] backgrounds = Resources.LoadAll<Sprite>("campus_background");
                if (backgrounds.Length > 0)
                    backgroundRenderer.sprite = backgrounds[0];
                backgroundRenderer.sortingOrder = -100;
                backgroundObject.transform.position = new Vector3(0f, 0f, 5f);
                MarkSceneArtworkDirty(backgroundObject);
            }
        }
        if (backgroundRenderer != null && backgroundRenderer.sprite == null)
        {
            Sprite[] backgrounds = Resources.LoadAll<Sprite>("campus_background");
            if (backgrounds.Length > 0)
                backgroundRenderer.sprite = backgrounds[0];
        }

        Camera previewCamera = Camera.main;
        if (previewCamera != null)
            FitBackgroundToCamera(previewCamera);
        else if (backgroundRenderer != null && backgroundRenderer.sprite != null)
            FitBackground(9f, 5.6f);
    }

    private void FitBackgroundToCamera(Camera targetCamera)
    {
        FitBackground(targetCamera.orthographicSize * 2f * targetCamera.aspect, targetCamera.orthographicSize * 2f);
        if (backgroundRenderer != null)
            backgroundRenderer.transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 5f);
    }

    private void FitBackground(float targetWidth, float targetHeight)
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            return;

        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        float scale = Mathf.Max(targetWidth / spriteSize.x, targetHeight / spriteSize.y);
        backgroundRenderer.transform.localScale = Vector3.one * scale;
    }

    private void MarkSceneArtworkDirty(GameObject createdObject)
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(createdObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void BuildPlayfield()
    {
        CreateBlock("Left Lane", new Vector3(-1.1f, 0f, 0f), new Vector3(.065f, 8.6f, 1f), new Color(.42f, .68f, 1f));
        CreateBlock("Right Lane", new Vector3(1.1f, 0f, 0f), new Vector3(.065f, 8.6f, 1f), new Color(.42f, .68f, 1f));
        CreateBlock("Player Line", new Vector3(0f, -2.35f, 0f), new Vector3(6.65f, .08f, 1f), new Color(.42f, .68f, 1f));
    }

    private void CreatePlayer()
    {
        player = CreateBlock("플레이어", new Vector3(laneX[lane], -1.35f, 0f), new Vector3(1.2f, 1.7f, 1f), new Color(.2f, .9f, 1f));
        playerRenderer = player.GetComponent<SpriteRenderer>();
        if (playerSprite != null)
        {
            playerRenderer.sprite = playerSprite;
            playerRenderer.color = Color.white;
            float maxSize = Mathf.Max(playerSprite.bounds.size.x, playerSprite.bounds.size.y);
            float targetHeight = 1.8f;
            player.transform.localScale = Vector3.one * (targetHeight / maxSize);
        }
    }

    private GameObject CreateBlock(string objectName, Vector3 position, Vector3 size, Color color)
    {
        GameObject block = new GameObject(objectName);
        block.transform.position = position;
        block.transform.localScale = size;
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = color;
        return block;
    }

    private void LoadItemSprite(ItemKind kind, string resourceName)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourceName);
        if (sprites.Length > 0)
            itemSprites[kind] = sprites[0];
        else
            Debug.LogWarning("Missing item sprite in Assets/Resources: " + resourceName + ".png");
    }

    private void Update()
    {
        if (state != GameState.Playing)
            return;

        float dt = Time.deltaTime;
        UpdatePlayerContactColor(dt);
        timeLeft -= dt;
        hitCooldown -= dt;
        reverseTimer -= dt;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            state = GameState.Won;
            return;
        }

        ReadMovement();
        player.transform.position = new Vector3(laneX[lane], -1.35f, 0f);

        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            SpawnItem();
            float difficulty = 1f - (45f - timeLeft) * .009f;
            spawnTimer = Random.Range(.72f, 1.05f) * Mathf.Clamp(difficulty, .58f, 1f);
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            FallingItem item = items[i];
            item.root.transform.position += Vector3.down * item.speed * obstacleSpeedBoost * dt;

            if (item.lane == lane && Mathf.Abs(item.root.transform.position.y - player.transform.position.y) < 1.7f)
            {
                ApplyItem(item.kind);
                Destroy(item.root);
                items.RemoveAt(i);
                if (state == GameState.Lost)
                    return;
            }
            else if (item.root.transform.position.y < -5.3f)
            {
                Destroy(item.root);
                items.RemoveAt(i);
            }
        }
    }

    private void ReadMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool left = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
        bool right = keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
        int direction = (right ? 1 : 0) - (left ? 1 : 0);
        if (reverseTimer > 0f)
            direction *= -1;

        if (direction != 0)
            lane = Mathf.Clamp(lane + direction, 0, 2);
    }

    private void SpawnItem()
    {
        int itemLane = Random.Range(0, 3);
        float roll = Random.value;
        ItemKind kind = roll < .42f ? ItemKind.Bomb
            : roll < .57f ? ItemKind.Meeting
            : roll < .70f ? ItemKind.Coffee
            : roll < .87f ? ItemKind.Contest : ItemKind.Club;

        Color color;
        switch (kind)
        {
            case ItemKind.Bomb: color = new Color(1f, .2f, .27f); break;
            case ItemKind.Meeting: color = new Color(.68f, .3f, 1f); break;
            case ItemKind.Coffee: color = new Color(.22f, .95f, .55f); break;
            case ItemKind.Contest: color = new Color(1f, .7f, .12f); break;
            default: color = new Color(.15f, .68f, 1f); break;
        }

        GameObject root = CreateBlock("아이템 - " + kind, new Vector3(laneX[itemLane], 3.1f, 0f), Vector3.one, Color.white);
        SpriteRenderer itemRenderer = root.GetComponent<SpriteRenderer>();
        Sprite itemSprite;
        if (itemSprites.TryGetValue(kind, out itemSprite))
        {
            itemRenderer.sprite = itemSprite;
            float maxSize = Mathf.Max(itemSprite.bounds.size.x, itemSprite.bounds.size.y);
            float targetSize = 1.6f;
            root.transform.localScale = Vector3.one * (targetSize / maxSize);
        }
        else
        {
            itemRenderer.color = color;
            root.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        }

        items.Add(new FallingItem
        {
            root = root,
            kind = kind,
            lane = itemLane,
            speed = Random.Range(2.3f, 3.0f) + (45f - timeLeft) * .025f
        });
    }

    private void ApplyItem(ItemKind kind)
    {
        SetPlayerContactColor(kind);
        switch (kind)
        {
            case ItemKind.Bomb:
                if (shieldHits > 0f) shieldHits = 0f;
                else if (hitCooldown <= 0f)
                {
                    hp--;
                    hitCooldown = 1f;
                    if (hp <= 0) state = GameState.Lost;
                }
                break;
            case ItemKind.Meeting:
                reverseTimer = 3f;
                break;
            case ItemKind.Coffee:
                shieldHits = 1f;
                break;
            case ItemKind.Contest:
                score += 200;
                break;
            case ItemKind.Club:
                score += 300;
                obstacleSpeedBoost *= 1.15f;
                break;
        }
    }

    private void SetPlayerContactColor(ItemKind kind)
    {
        if (kind != ItemKind.Bomb && kind != ItemKind.Meeting)
            return;

        playerContactColor = new Color(1f, .18f, .22f);
        playerColorTimer = .4f;
        if (playerRenderer != null)
            playerRenderer.color = playerContactColor;
    }

    private void UpdatePlayerContactColor(float deltaTime)
    {
        if (playerRenderer == null)
            return;

        if (playerColorTimer > 0f)
        {
            playerColorTimer -= deltaTime;
            playerRenderer.color = Color.Lerp(Color.white, playerContactColor, Mathf.Clamp01(playerColorTimer / .4f));
        }
        else
        {
            playerRenderer.color = Color.white;
        }
    }

    private void ShowInstructions()
    {
        state = GameState.Instructions;
    }

    private void BeginGame()
    {
        ClearItems();
        lane = 1;
        hp = 3;
        score = 0;
        timeLeft = 45f;
        spawnTimer = .4f;
        shieldHits = 0f;
        reverseTimer = 0f;
        hitCooldown = 0f;
        obstacleSpeedBoost = 1f;
        playerColorTimer = 0f;
        if (playerRenderer != null) playerRenderer.color = Color.white;
        player.transform.position = new Vector3(laneX[lane], -1.35f, 0f);
        state = GameState.Playing;
    }

    private void ClearItems()
    {
        foreach (FallingItem item in items)
            if (item.root != null) Destroy(item.root);
        items.Clear();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        EnsureUiTextures();

        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Screen.height / 20, 22, 34),
            fontStyle = FontStyle.Bold,
            richText = true,
            normal = { textColor = Color.white }
        };
        if (koreanFont != null) label.font = koreanFont;

        if (state == GameState.Playing)
        {
            GUI.Label(new Rect(12, 12, Screen.width - 24, 56),
                "남은 시간  " + Mathf.CeilToInt(timeLeft) + "초     체력  " + hp + "     스펙  " + score +
                (shieldHits > 0 ? "     보호막" : "") +
                (reverseTimer > 0 ? "     <color=#FF4D5A>긴급회의! 좌우 반전 " + reverseTimer.ToString("0.0") + "초 — 반대 방향 키</color>" : ""), label);
            return;
        }

        Rect panelRect = new Rect(Screen.width * .08f, Screen.height * .1f, Screen.width * .84f, Screen.height * .8f);
        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = opaquePanelTexture;
        panelStyle.border = new RectOffset(12, 12, 12, 12);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUIStyle titleStyle = new GUIStyle(label) { fontSize = Mathf.Clamp(Screen.height / 15, 34, 48) };
        GUIStyle bodyStyle = new GUIStyle(label)
        {
            fontSize = Mathf.Clamp(Screen.height / 27, 19, 25),
            wordWrap = true,
            fontStyle = FontStyle.Normal
        };
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.Clamp(Screen.height / 25, 20, 30),
            fontStyle = FontStyle.Bold,
            normal = { background = buttonTexture, textColor = Color.white },
            hover = { background = buttonTexture, textColor = Color.white },
            active = { background = buttonTexture, textColor = Color.white }
        };
        if (koreanFont != null)
        {
            titleStyle.font = koreanFont;
            bodyStyle.font = koreanFont;
            buttonStyle.font = koreanFont;
        }

        string title;
        string details;
        string buttonText;
        System.Action buttonAction;

        if (state == GameState.Ready)
        {
            title = titlePreview != null ? titlePreview.text : gameTitle;
            details = "갓생 대학생의 45초 생존기\n게임 방법과 아이템 설명을 확인해보세요.";
            buttonText = "게임 시작";
            buttonAction = ShowInstructions;
        }
        else if (state == GameState.Instructions)
        {
            title = "게임 설명";
            details = "조작  ← → 또는 A / D 로 세 레인 이동\n목표  체력 3으로 45초 생존하기\n\n팀플 폭탄  체력 -1\n학회 긴급회의  3초간 좌우 조작 반전\n아이스 아메리카노  다음 피해 1회 방어\n공모전 수상  스펙 +200점\n대외활동 합격  스펙 +300점, 이후 장애물 속도 증가";
            buttonText = "플레이 시작";
            buttonAction = BeginGame;
        }
        else
        {
            title = state == GameState.Won ? "오늘도 살아남았다!" : "오늘은 여기까지...";
            details = "스펙 점수  " + score + "\n" + (state == GameState.Won ? (score >= 500 ? "갓생 마스터!" : "일단 살아남은 대학생") : "다시 도전해봐요!");
            buttonText = "설명 보고 다시 하기";
            buttonAction = ShowInstructions;
        }

        GUI.Label(new Rect(panelRect.x + 20, panelRect.y + 30, panelRect.width - 40, 72), title, titleStyle);
        if (state == GameState.Instructions)
            DrawInstructionRows(panelRect, bodyStyle);
        else
            GUI.Label(new Rect(panelRect.x + 30, panelRect.y + 115, panelRect.width - 60, panelRect.height - 205), details, bodyStyle);
        if (GUI.Button(new Rect(Screen.width * .32f, panelRect.yMax - 88, Screen.width * .36f, 58), buttonText, buttonStyle))
            buttonAction();
    }

    private void DrawInstructionRows(Rect panelRect, GUIStyle bodyStyle)
    {
        GUI.Label(new Rect(panelRect.x + 30, panelRect.y + 100, panelRect.width - 60, 62),
            "조작  ← → 또는 A / D 로 이동     목표  체력 3으로 45초 생존", bodyStyle);

        ItemKind[] order = { ItemKind.Bomb, ItemKind.Meeting, ItemKind.Coffee, ItemKind.Contest, ItemKind.Club };
        string[] descriptions =
        {
            "팀플 폭탄 — 체력 1 감소",
            "학회 긴급회의 — 3초간 좌우 반전",
            "아이스 아메리카노 — 다음 피해 방어",
            "공모전 수상 — 스펙 +200점",
            "대외활동 합격 — 스펙 +300점, 장애물 속도 증가"
        };

        float buttonTop = panelRect.yMax - 88f;
        float rowsTop = panelRect.y + 164f;
        float rowHeight = (buttonTop - rowsTop - 8f) / order.Length;
        float iconSize = Mathf.Clamp(rowHeight - 4f, 34f, 48f);
        float iconX = panelRect.x + 32f;

        for (int i = 0; i < order.Length; i++)
        {
            float rowY = rowsTop + i * rowHeight;
            Sprite sprite;
            if (itemSprites.TryGetValue(order[i], out sprite) && sprite != null)
            {
                Rect iconRect = new Rect(iconX, rowY + (rowHeight - iconSize) * .5f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, sprite.texture, ScaleMode.ScaleToFit, true);
            }

            Rect textRect = new Rect(iconX + iconSize + 6f, rowY, panelRect.xMax - iconX - iconSize - 28f, rowHeight);
            GUI.Label(textRect, descriptions[i], bodyStyle);
        }
    }

    private void EnsureUiTextures()
    {
        if (opaquePanelTexture == null)
            opaquePanelTexture = CreateUiTexture(new Color(.015f, .025f, .055f, .99f));
        if (buttonTexture == null)
            buttonTexture = CreateUiTexture(new Color(.08f, .34f, .62f, 1f));
    }

    private Texture2D CreateUiTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
