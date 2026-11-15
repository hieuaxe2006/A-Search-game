using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AStarHexGrid : MonoBehaviour
{
    // ========================= CONSTANTS =========================
    private const int Columns = 10;
    private const int Rows = 20;
    private const int MaxBlocks = 5;
    private const float CellSize = 0.55f;
    private const float RowSpacing = 0.8660254f;

    // ========================= ENUMS =========================
    private enum Role { Angel, Evil }

    // ========================= INSPECTOR FIELDS =========================
    // Giữ nguyên tên để không mất reference trong Inspector
    [Header("Map Setup (x: 0-9, y: 0-19)")]
    [Tooltip("Vị trí xuất phát của NPC hồn ma. Hàng y=19 là hàng đích.")]
    public Vector2Int playerStartPosition = new Vector2Int(2, 1);
    [Tooltip("(Không sử dụng — giữ để tương thích Inspector)")]
    public Vector2Int aiStartPosition = new Vector2Int(4, 0);
    [Tooltip("Danh sách ô núi cố định, không thể đi qua.")]
    public List<Vector2Int> fixedBlockPositions = new List<Vector2Int>
    {
        new Vector2Int(1, 0), new Vector2Int(7, 0),
        new Vector2Int(1, 1), new Vector2Int(5, 1), new Vector2Int(8, 1),
        new Vector2Int(2, 3), new Vector2Int(5, 3), new Vector2Int(7, 3), new Vector2Int(8, 3),
        new Vector2Int(4, 4), new Vector2Int(9, 4),
        new Vector2Int(5, 5), new Vector2Int(7, 5),
        new Vector2Int(0, 6), new Vector2Int(1, 6), new Vector2Int(3, 6),
        new Vector2Int(4, 6), new Vector2Int(9, 6),
        new Vector2Int(7, 7),
        new Vector2Int(3, 8), new Vector2Int(6, 8),
        new Vector2Int(0, 9), new Vector2Int(3, 9), new Vector2Int(7, 9), new Vector2Int(9, 9),
        new Vector2Int(2, 10),
        new Vector2Int(0, 11), new Vector2Int(1, 11), new Vector2Int(6, 11), new Vector2Int(8, 11),
        new Vector2Int(4, 12), new Vector2Int(5, 12), new Vector2Int(7, 12),
        new Vector2Int(2, 13), new Vector2Int(3, 13), new Vector2Int(7, 13), new Vector2Int(8, 13),
        new Vector2Int(1, 14), new Vector2Int(9, 14),
        new Vector2Int(2, 15), new Vector2Int(6, 15),
        new Vector2Int(0, 16), new Vector2Int(3, 16), new Vector2Int(5, 16), new Vector2Int(8, 16),
        new Vector2Int(2, 17), new Vector2Int(7, 17),
        new Vector2Int(0, 18), new Vector2Int(9, 18)
    };
    public List<Vector2Int> stonePositions = new List<Vector2Int>
    {
        new Vector2Int(2, 1), new Vector2Int(6, 1), new Vector2Int(3, 5),
        new Vector2Int(7, 8), new Vector2Int(2, 11), new Vector2Int(5, 13), new Vector2Int(4, 16)
    };
    public List<Vector2Int> sandPositions = new List<Vector2Int>
    {
        new Vector2Int(7, 1), new Vector2Int(2, 5), new Vector2Int(4, 8),
        new Vector2Int(6, 11), new Vector2Int(1, 13), new Vector2Int(7, 16), new Vector2Int(3, 17)
    };

    [Header("Hex Terrain Sprites")]
    public Sprite grassTileSprite;
    public Sprite stoneTileSprite;
    public Sprite sandTileSprite;
    public Sprite mountainTileSprite;
    public Sprite waterTileSprite;
    public Sprite compositionGoalSprite;

    [Header("Character Animation Frames (dùng cho NPC hồn ma)")]
    public Sprite[] playerIdleFrames;
    public Sprite[] playerRunFrames;
    public Sprite[] aiIdleFrames;  // giữ để tương thích Inspector
    public Sprite[] aiRunFrames;   // giữ để tương thích Inspector

    // ========================= INNER CLASS =========================
    private class BoardCell
    {
        public Vector2Int position;
        public SpriteRenderer renderer;
    }

    // ========================= BOARD DATA =========================
    private readonly BoardCell[,] cells = new BoardCell[Columns, Rows];
    private readonly bool[,] fixedWalls = new bool[Columns, Rows];
    private readonly bool[,] playerWalls = new bool[Columns, Rows];
    private readonly bool[,] aiWalls = new bool[Columns, Rows];
    private readonly bool[,] stoneTiles = new bool[Columns, Rows];
    private readonly bool[,] sandTiles = new bool[Columns, Rows];
    private readonly Dictionary<Collider2D, Vector2Int> cellByCollider = new Dictionary<Collider2D, Vector2Int>();
    private readonly HashSet<Vector2Int> npcPathCells = new HashSet<Vector2Int>();

    // ========================= CELL VALUES =========================
    private readonly int[,] cellValues = new int[Columns, Rows];
    private readonly bool[,] cellIsSecret = new bool[Columns, Rows];
    private readonly bool[,] cellRevealed = new bool[Columns, Rows];
    private TextMeshPro[,] cellValueTexts;

    // ========================= GAME STATE =========================
    private Role playerRole;
    private Role aiRole;
    private Vector2Int npcPosition;
    private int playerBlocksPlaced;
    private int aiBlocksPlaced;
    private int fixedWallCount;
    private int currentScore;
    private int turnNumber = 1;
    private bool isPlayerTurn = true;
    private bool isGameOver;
    private bool gameStarted;
    private bool npcMoving;

    // ========================= NPC TOKEN =========================
    private Camera gameCamera;
    private GameObject npcToken;
    private SpriteRenderer npcSpriteRenderer;
    private Coroutine npcAnimCoroutine;
    private Sprite squareSprite;
    private Sprite circleSprite;

    // ========================= UI =========================
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI projectedText;
    private TextMeshProUGUI blocksText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI roleInfoText;
    private Button skipButton;
    private Image skipButtonImage;
    private GameObject roleSelectionPanel;
    private GameObject endOverlay;
    private GameObject sidePanelObj;
    private Coroutine turnRoutine;
    private TMP_FontAsset uiFont;

    // ========================= HEX NEIGHBORS =========================
    private static readonly Vector2Int[] evenColumnNeighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
        new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1)
    };
    private static readonly Vector2Int[] oddColumnNeighbors =
    {
        new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(0, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
    };

    // ================================================================
    //                         LIFECYCLE
    // ================================================================

    private void Start()
    {
        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;
        npcPosition = ClampToBoard(playerStartPosition);

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            canvas.gameObject.SetActive(false);

        gameCamera = Camera.main;
        if (gameCamera != null)
        {
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 5.45f;
            gameCamera.backgroundColor = new Color(0.025f, 0.035f, 0.05f, 1f);
            gameCamera.transform.position = new Vector3(0f, 0f, -10f);
            CameraController cameraController = gameCamera.GetComponent<CameraController>();
            if (cameraController != null) cameraController.enabled = false;
        }

        CreateSprites();
        CreateFixedWalls();
        CreateTerrainLists();
        CreateBoard();
        GenerateCellValues();
        CreateCellValueTexts();
        CreateNPCToken();
        CreateInterface();
        CreateRoleSelection();
    }

    private void Update()
    {
        if (!gameStarted || isGameOver || !isPlayerTurn || npcMoving) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = gameCamera.ScreenToWorldPoint(screenPosition);
        Collider2D clickedCollider = Physics2D.OverlapPoint(worldPosition);
        if (clickedCollider == null || !cellByCollider.TryGetValue(clickedCollider, out Vector2Int clickedCell))
            return;

        TryPlacePlayerBlock(clickedCell);
    }

    // ================================================================
    //                      SPRITE CREATION
    // ================================================================

    private void CreateSprites()
    {
        Texture2D squareTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        squareTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        squareTexture.Apply();
        squareSprite = Sprite.Create(squareTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);

        const int textureSize = 64;
        Texture2D circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.47f;
        for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * textureSize + x] = distance <= radius ? Color.white : Color.clear;
            }
        circleTexture.SetPixels(pixels);
        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f), textureSize);
    }

    // ================================================================
    //                       BOARD SETUP
    // ================================================================

    private void CreateFixedWalls()
    {
        HashSet<Vector2Int> uniquePositions = new HashSet<Vector2Int>();
        foreach (Vector2Int position in fixedBlockPositions)
        {
            if (!IsInside(position) || position.y == Rows - 1 ||
                position == npcPosition || !uniquePositions.Add(position))
                continue;
            fixedWalls[position.x, position.y] = true;
            fixedWallCount++;
        }
    }

    private void CreateTerrainLists()
    {
        foreach (Vector2Int position in stonePositions)
            if (IsInside(position) && !fixedWalls[position.x, position.y] && position.y < Rows - 1)
                stoneTiles[position.x, position.y] = true;

        foreach (Vector2Int position in sandPositions)
            if (IsInside(position) && !fixedWalls[position.x, position.y] &&
                !stoneTiles[position.x, position.y] && position.y < Rows - 1)
                sandTiles[position.x, position.y] = true;
    }

    private void CreateBoard()
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GameObject tile = new GameObject("Tile_" + x + "_" + y);
                tile.transform.SetParent(transform, false);
                tile.transform.position = CellToWorld(position);

                SpriteRenderer tileRenderer = tile.AddComponent<SpriteRenderer>();
                tileRenderer.sprite = GetTerrainSprite(position);
                tileRenderer.color = Color.white;
                tileRenderer.sortingOrder = 0;
                float spriteWidth = tileRenderer.sprite != null ? tileRenderer.sprite.bounds.size.x : 1f;
                float spriteScale = CellSize / Mathf.Max(spriteWidth, 0.001f);
                tile.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);

                float colliderRadius = Mathf.Max(spriteWidth, 0.001f) * 0.5f;
                PolygonCollider2D collider = tile.AddComponent<PolygonCollider2D>();
                collider.SetPath(0, new[]
                {
                    new Vector2(-colliderRadius, 0f),
                    new Vector2(-colliderRadius * 0.5f, colliderRadius * RowSpacing),
                    new Vector2(colliderRadius * 0.5f, colliderRadius * RowSpacing),
                    new Vector2(colliderRadius, 0f),
                    new Vector2(colliderRadius * 0.5f, -colliderRadius * RowSpacing),
                    new Vector2(-colliderRadius * 0.5f, -colliderRadius * RowSpacing)
                });
                cellByCollider.Add(collider, position);
                cells[x, y] = new BoardCell { position = position, renderer = tileRenderer };
            }
        }
    }

    private Sprite GetTerrainSprite(Vector2Int position)
    {
        if (fixedWalls[position.x, position.y]) return mountainTileSprite != null ? mountainTileSprite : squareSprite;
        if (position.y == Rows - 1) return compositionGoalSprite != null ? compositionGoalSprite : squareSprite;
        if (stoneTiles[position.x, position.y]) return stoneTileSprite != null ? stoneTileSprite : squareSprite;
        if (sandTiles[position.x, position.y]) return sandTileSprite != null ? sandTileSprite : squareSprite;
        return grassTileSprite != null ? grassTileSprite : squareSprite;
    }

    // ================================================================
    //                       CELL VALUES
    // ================================================================

    private void GenerateCellValues()
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                // Không gán giá trị cho: tường, hàng đích, ô xuất phát NPC
                if (fixedWalls[x, y] || y == Rows - 1 || new Vector2Int(x, y) == npcPosition)
                {
                    cellValues[x, y] = 0;
                    continue;
                }

                float roll = Random.value;
                if (roll < 0.15f)
                {
                    // 15% — ô trống
                    cellValues[x, y] = 0;
                }
                else if (roll < 0.35f)
                {
                    // 20% — ô bí mật (secret)
                    cellValues[x, y] = Random.Range(1, 10);
                    cellIsSecret[x, y] = true;
                }
                else
                {
                    // 65% — ô hiển thị số
                    cellValues[x, y] = Random.Range(1, 10);
                }
            }
        }
    }

    private void CreateCellValueTexts()
    {
        cellValueTexts = new TextMeshPro[Columns, Rows];
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                if (fixedWalls[x, y] || y == Rows - 1) continue;

                Vector2Int pos = new Vector2Int(x, y);
                GameObject textObj = new GameObject("Val_" + x + "_" + y);
                textObj.transform.SetParent(transform, false);
                textObj.transform.position = CellToWorld(pos) + new Vector3(0f, 0f, -0.05f);

                TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
                tmp.font = uiFont;
                tmp.fontSize = 3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(CellSize * 0.9f, CellSize * 0.6f);
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.text = "";

                // Sorting — đặt trên tile sprite
                Renderer r = textObj.GetComponent<Renderer>();
                if (r != null) r.sortingOrder = 3;

                cellValueTexts[x, y] = tmp;
            }
        }
    }

    private void UpdateCellValueDisplays()
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                TextMeshPro tmp = cellValueTexts != null && x < Columns && y < Rows ? cellValueTexts[x, y] : null;
                if (tmp == null) continue;

                int value = cellValues[x, y];
                bool secret = cellIsSecret[x, y] && !cellRevealed[x, y];

                if (value == 0 && !secret)
                {
                    tmp.text = "";
                }
                else if (secret)
                {
                    tmp.text = "?";
                    tmp.color = new Color(0.7f, 0.7f, 0.7f);
                }
                else
                {
                    tmp.text = value.ToString();
                    // Chẵn = xanh (Angel), Lẻ = đỏ (Evil)
                    tmp.color = (value % 2 == 0)
                        ? new Color(0.2f, 0.7f, 1f)
                        : new Color(1f, 0.4f, 0.35f);
                }
            }
        }
    }

    // ================================================================
    //                       NPC TOKEN
    // ================================================================

    private void CreateNPCToken()
    {
        npcToken = CreateToken("NPC_Ghost", playerIdleFrames, new Color(0.6f, 0.2f, 0.85f));
        npcToken.SetActive(false); // Ẩn cho tới khi chọn vai
        UpdateNPCPosition();
    }

    private GameObject CreateToken(string objectName, Sprite[] idleFrames, Color fallbackColor)
    {
        GameObject token = new GameObject(objectName);
        token.transform.SetParent(transform, false);
        npcSpriteRenderer = token.AddComponent<SpriteRenderer>();
        Sprite firstFrame = (idleFrames != null && idleFrames.Length > 0) ? idleFrames[0] : null;
        npcSpriteRenderer.sprite = firstFrame != null ? firstFrame : circleSprite;
        npcSpriteRenderer.color = firstFrame != null ? Color.white : fallbackColor;
        npcSpriteRenderer.sortingOrder = 5;
        float spriteHeight = npcSpriteRenderer.sprite != null ? npcSpriteRenderer.sprite.bounds.size.y : 0f;
        float targetHeight = CellSize * 1.25f;
        float scale = spriteHeight > 0.001f ? targetHeight / spriteHeight : CellSize * 0.7f;
        token.transform.localScale = Vector3.one * scale;
        return token;
    }

    private void UpdateNPCPosition()
    {
        if (npcToken != null && !npcMoving)
            npcToken.transform.position = CellToWorld(npcPosition) + new Vector3(0f, 0f, -0.1f);
    }

    // ================================================================
    //                       ANIMATION
    // ================================================================

    private IEnumerator PlayIdleAnimation(SpriteRenderer renderer, Sprite[] frames)
    {
        if (renderer == null || frames == null || frames.Length == 0) yield break;
        int frameIndex = 0;
        while (renderer != null)
        {
            renderer.sprite = frames[frameIndex];
            frameIndex = (frameIndex + 1) % frames.Length;
            yield return new WaitForSeconds(0.12f);
        }
    }

    private IEnumerator AnimateNPCMove(Vector3 start, Vector3 destination)
    {
        if (npcToken == null || npcSpriteRenderer == null) yield break;

        const float moveDuration = 0.42f;
        const float frameDuration = 0.07f;
        float elapsed = 0f;
        float nextFrameTime = 0f;
        int frameIndex = 0;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            if (playerRunFrames != null && playerRunFrames.Length > 0 && elapsed >= nextFrameTime)
            {
                npcSpriteRenderer.sprite = playerRunFrames[frameIndex];
                frameIndex = (frameIndex + 1) % playerRunFrames.Length;
                nextFrameTime += frameDuration;
            }
            npcToken.transform.position = Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / moveDuration));
            yield return null;
        }

        npcToken.transform.position = destination;
    }

    // ================================================================
    //                       HEX MATH
    // ================================================================

    private Vector3 CellToWorld(Vector2Int position)
    {
        float x = (position.x - (Columns - 1) * 0.5f) * CellSize * 0.75f;
        float offsetRow = position.y + ((position.x & 1) * 0.5f);
        float centerRow = (Rows - 1) * 0.5f + 0.25f;
        float y = (offsetRow - centerRow) * CellSize * RowSpacing;
        return new Vector3(x, y, 0f);
    }

    private Vector2Int[] GetNeighborOffsets(Vector2Int position)
    {
        return (position.x & 1) == 0 ? evenColumnNeighbors : oddColumnNeighbors;
    }

    private bool AreHexNeighbors(Vector2Int a, Vector2Int b)
    {
        foreach (Vector2Int offset in GetNeighborOffsets(a))
            if (a + offset == b) return true;
        return false;
    }

    // ================================================================
    //                       BOARD REFRESH
    // ================================================================

    private void RefreshBoard()
    {
        // Tính đường A* hiện tại của NPC
        npcPathCells.Clear();
        List<Vector2Int> path = FindPath(npcPosition);
        if (path != null)
            foreach (Vector2Int pos in path)
                npcPathCells.Add(pos);

        // Cập nhật từng ô
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                SpriteRenderer renderer = cells[x, y].renderer;
                bool dynamicWater = playerWalls[x, y] || aiWalls[x, y];

                renderer.sprite = dynamicWater && waterTileSprite != null
                    ? waterTileSprite
                    : GetTerrainSprite(position);

                renderer.color = npcPathCells.Contains(position) &&
                                 position != npcPosition &&
                                 position.y < Rows - 1 &&
                                 !fixedWalls[x, y] && !dynamicWater
                    ? new Color(1f, 0.85f, 0.7f) // Cam nhạt cho đường đi dự kiến
                    : Color.white;
            }
        }

        UpdateCellValueDisplays();
        UpdateNPCPosition();

        // Cập nhật projected score
        if (projectedText != null && path != null)
        {
            int projected = currentScore + CalculatePathScore(path);
            bool isEven = projected % 2 == 0;
            string parityLabel = isEven ? "Chẵn" : "Lẻ";
            string winnerLabel = isEven ? "Angel" : "Evil";
            projectedText.text = "Dự kiến: " + projected + " (" + parityLabel + " → " + winnerLabel + ")";
            projectedText.color = isEven
                ? new Color(0.4f, 0.75f, 1f)
                : new Color(1f, 0.5f, 0.45f);
        }
    }

    // ================================================================
    //                           UI
    // ================================================================

    private void CreateInterface()
    {
        uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (uiFont == null && TMP_Settings.instance != null)
            uiFont = TMP_Settings.defaultFontAsset;

        GameObject canvasObject = new GameObject("GameCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        // Side panel
        sidePanelObj = CreatePanel("ControlPanel", canvasObject.transform,
            new Color(0.035f, 0.05f, 0.07f, 0.96f));
        RectTransform panelRect = sidePanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(350f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        sidePanelObj.SetActive(false); // Ẩn cho tới khi chọn vai

        float yPos = -30f;

        CreateText(sidePanelObj.transform, "A* SEARCH GAME", 30f,
            new Vector2(0f, yPos), new Vector2(310f, 45f),
            new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        yPos -= 50f;

        CreateText(sidePanelObj.transform, "HEX 10 × 20  •  SCORE STRATEGY", 14f,
            new Vector2(0f, yPos), new Vector2(310f, 25f),
            new Color(0.58f, 0.68f, 0.78f), TextAlignmentOptions.Center, FontStyles.Normal);
        yPos -= 40f;

        // Vai trò
        roleInfoText = CreateText(sidePanelObj.transform, "", 20f,
            new Vector2(0f, yPos), new Vector2(310f, 35f),
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        yPos -= 45f;

        // Score hiện tại
        scoreText = CreateText(sidePanelObj.transform, "SCORE: 0", 28f,
            new Vector2(0f, yPos), new Vector2(310f, 45f),
            new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
        yPos -= 45f;

        // Projected score
        projectedText = CreateText(sidePanelObj.transform, "", 16f,
            new Vector2(0f, yPos), new Vector2(310f, 30f),
            new Color(0.7f, 0.8f, 0.9f), TextAlignmentOptions.Center, FontStyles.Normal);
        yPos -= 45f;

        // Lượt
        turnText = CreateText(sidePanelObj.transform, "", 21f,
            new Vector2(0f, yPos), new Vector2(310f, 35f),
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        yPos -= 42f;

        // Blocks còn lại
        blocksText = CreateText(sidePanelObj.transform, "", 16f,
            new Vector2(0f, yPos), new Vector2(310f, 45f),
            new Color(0.78f, 0.84f, 0.9f), TextAlignmentOptions.Center, FontStyles.Normal);
        yPos -= 55f;

        // Hint
        hintText = CreateText(sidePanelObj.transform, "", 15f,
            new Vector2(0f, yPos), new Vector2(300f, 65f),
            new Color(0.64f, 0.74f, 0.83f), TextAlignmentOptions.Center, FontStyles.Normal);
        yPos -= 80f;

        // Nút bỏ qua lượt
        skipButton = CreateButton(sidePanelObj.transform, "SkipButton", "BỎ QUA LƯỢT",
            new Vector2(0f, yPos), out skipButtonImage, out _);
        skipButtonImage.color = new Color(0.16f, 0.49f, 0.37f);
        skipButton.onClick.AddListener(SkipTurn);
        yPos -= 85f;

        // Luật chơi
        CreateText(sidePanelObj.transform,
            "Bấm ô trống để đặt chặn (tối đa 5).\n" +
            "NPC hồn ma đi theo A* tới đích.\n" +
            "Score chẵn → Angel thắng.\n" +
            "Score lẻ → Evil thắng.\n" +
            "Số xanh = chẵn, Số đỏ = lẻ, ? = bí mật.",
            12f, new Vector2(0f, yPos), new Vector2(300f, 110f),
            new Color(0.46f, 0.56f, 0.65f), TextAlignmentOptions.Center, FontStyles.Normal);
    }

    // ================================================================
    //                     ROLE SELECTION
    // ================================================================

    private void CreateRoleSelection()
    {
        GameObject canvasObj = GameObject.Find("GameCanvas");
        if (canvasObj == null) return;

        roleSelectionPanel = CreatePanel("RoleSelection", canvasObj.transform,
            new Color(0.02f, 0.03f, 0.05f, 0.95f));
        RectTransform rt = roleSelectionPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Card trung tâm
        GameObject card = CreatePanel("SelectCard", roleSelectionPanel.transform,
            new Color(0.06f, 0.08f, 0.12f, 1f));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(550f, 420f);
        cardRect.anchoredPosition = Vector2.zero;

        CreateText(card.transform, "A* SEARCH GAME", 36f,
            new Vector2(0f, -30f), new Vector2(480f, 55f),
            new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

        CreateText(card.transform, "NPC hồn ma sẽ đi theo thuật toán A*.\n" +
            "Bạn và AI thay phiên đặt chướng ngại vật\nđể hướng NPC qua các ô có lợi cho phe mình.", 16f,
            new Vector2(0f, -95f), new Vector2(480f, 75f),
            new Color(0.7f, 0.78f, 0.86f), TextAlignmentOptions.Center, FontStyles.Normal);

        CreateText(card.transform, "CHỌN PHE CỦA BẠN", 22f,
            new Vector2(0f, -185f), new Vector2(480f, 40f),
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Nút Angel
        Button angelBtn = CreateButton(card.transform, "AngelBtn", "ANGEL  (Score chẵn → Thắng)",
            new Vector2(0f, -240f), out Image angelBg, out _);
        angelBg.color = new Color(0.12f, 0.4f, 0.7f);
        angelBtn.onClick.AddListener(() => SelectRole(Role.Angel));

        // Nút Evil
        Button evilBtn = CreateButton(card.transform, "EvilBtn", "EVIL  (Score lẻ → Thắng)",
            new Vector2(0f, -320f), out Image evilBg, out _);
        evilBg.color = new Color(0.65f, 0.18f, 0.18f);
        evilBtn.onClick.AddListener(() => SelectRole(Role.Evil));
    }

    private void SelectRole(Role role)
    {
        playerRole = role;
        aiRole = (role == Role.Angel) ? Role.Evil : Role.Angel;

        if (roleSelectionPanel != null)
            roleSelectionPanel.SetActive(false);

        // Hiện game
        npcToken.SetActive(true);
        sidePanelObj.SetActive(true);
        npcAnimCoroutine = StartCoroutine(PlayIdleAnimation(npcSpriteRenderer, playerIdleFrames));

        gameStarted = true;
        isPlayerTurn = true;
        RefreshBoard();
        RefreshHud("Lượt bạn! Bấm ô trống để đặt chặn, hoặc Bỏ qua.");
    }

    // ================================================================
    //                     UI HELPERS
    // ================================================================

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.sprite = squareSprite;
        image.color = color;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string value, float size, Vector2 position,
        Vector2 dimensions, Color color, TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = uiFont;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 position,
        out Image background, out TextMeshProUGUI labelText)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(286f, 66f);

        background = buttonObject.GetComponent<Image>();
        background.sprite = squareSprite;
        background.color = new Color(0.12f, 0.2f, 0.29f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        labelText = CreateText(buttonObject.transform, label, 17f, Vector2.zero,
            new Vector2(270f, 66f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        return button;
    }

    private void RefreshHud(string message)
    {
        if (turnText != null)
        {
            turnText.text = isPlayerTurn
                ? "LƯỢT " + turnNumber + "  •  BẠN"
                : "LƯỢT " + turnNumber + "  •  AI";
            turnText.color = isPlayerTurn
                ? new Color(0.42f, 0.75f, 1f)
                : new Color(1f, 0.48f, 0.5f);
        }

        if (scoreText != null)
        {
            scoreText.text = "SCORE: " + currentScore;
            bool isEven = currentScore % 2 == 0;
            scoreText.color = isEven
                ? new Color(0.4f, 0.8f, 1f)
                : new Color(1f, 0.5f, 0.4f);
        }

        if (roleInfoText != null)
        {
            string roleName = playerRole == Role.Angel ? "ANGEL" : "EVIL";
            string condition = playerRole == Role.Angel ? "Score chẵn → Bạn thắng" : "Score lẻ → Bạn thắng";
            roleInfoText.text = "Phe: " + roleName + "\n" + condition;
            roleInfoText.color = playerRole == Role.Angel
                ? new Color(0.4f, 0.75f, 1f)
                : new Color(1f, 0.45f, 0.4f);
        }

        if (blocksText != null)
            blocksText.text = "Block còn lại — Bạn: " + (MaxBlocks - playerBlocksPlaced) +
                              "/5   AI: " + (MaxBlocks - aiBlocksPlaced) + "/5";

        if (hintText != null)
            hintText.text = message;

        if (skipButton != null)
            skipButton.interactable = isPlayerTurn && !isGameOver && !npcMoving;
    }

    // ================================================================
    //                     PLAYER ACTIONS
    // ================================================================

    private void TryPlacePlayerBlock(Vector2Int position)
    {
        if (npcMoving) return;

        if (playerBlocksPlaced >= MaxBlocks)
        {
            RefreshHud("Bạn đã hết block! Bấm Bỏ qua lượt.");
            return;
        }

        if (position == npcPosition)
        {
            RefreshHud("Không thể đặt chặn trên NPC.");
            return;
        }

        if (position.y == Rows - 1)
        {
            RefreshHud("Không thể chặn hàng đích.");
            return;
        }

        if (IsWall(position))
        {
            RefreshHud("Ô này đã có tường.");
            return;
        }

        playerWalls[position.x, position.y] = true;
        if (!IsBoardLayoutLegal())
        {
            playerWalls[position.x, position.y] = false;
            RefreshHud("Chặn này sẽ khóa đường NPC tới đích!");
            return;
        }

        playerBlocksPlaced++;
        RefreshBoard();
        RefreshHud("Đã đặt chặn! NPC đang di chuyển...");

        // Bắt đầu chuỗi: NPC đi → AI đặt block → NPC đi
        turnRoutine = StartCoroutine(AfterPlayerAction());
    }

    private void SkipTurn()
    {
        if (isGameOver || !isPlayerTurn || npcMoving) return;
        RefreshHud("Bỏ qua! NPC đang di chuyển...");
        turnRoutine = StartCoroutine(AfterPlayerAction());
    }

    // ================================================================
    //                       GAME FLOW
    // ================================================================

    private IEnumerator AfterPlayerAction()
    {
        isPlayerTurn = false;

        // NPC di chuyển 1 bước sau lượt Player
        yield return StartCoroutine(MoveNPCOneStep());
        if (isGameOver) yield break;

        // AI đặt block + NPC di chuyển 1 bước
        yield return StartCoroutine(PlayAiTurn());
        if (isGameOver) yield break;

        // Lượt mới cho Player
        turnNumber++;
        isPlayerTurn = true;
        RefreshHud(playerBlocksPlaced >= MaxBlocks
            ? "Bạn đã hết block. Bấm Bỏ qua để tiếp tục."
            : "Lượt bạn! Bấm ô trống để đặt chặn, hoặc Bỏ qua.");
        turnRoutine = null;
    }

    private IEnumerator MoveNPCOneStep()
    {
        List<Vector2Int> path = FindPath(npcPosition);
        if (path == null || path.Count <= 1)
        {
            // NPC đã ở đích hoặc bị kẹt (không nên xảy ra)
            if (npcPosition.y == Rows - 1) EndGame();
            yield break;
        }

        npcMoving = true;
        Vector2Int nextCell = path[1];
        Vector3 startPos = npcToken.transform.position;
        Vector3 destPos = CellToWorld(nextCell) + new Vector3(0f, 0f, -0.1f);

        // Dừng idle animation
        if (npcAnimCoroutine != null) StopCoroutine(npcAnimCoroutine);

        // Animate di chuyển
        yield return StartCoroutine(AnimateNPCMove(startPos, destPos));

        // Cập nhật vị trí
        npcPosition = nextCell;

        // Xử lý giá trị ô
        int val = cellValues[nextCell.x, nextCell.y];
        if (val != 0)
        {
            // Reveal nếu là secret
            if (cellIsSecret[nextCell.x, nextCell.y] && !cellRevealed[nextCell.x, nextCell.y])
                cellRevealed[nextCell.x, nextCell.y] = true;

            currentScore += val;
            // Xóa giá trị ô sau khi đã cộng (NPC đã lấy)
            cellValues[nextCell.x, nextCell.y] = 0;
        }

        npcMoving = false;

        // Khởi động lại idle animation
        npcAnimCoroutine = StartCoroutine(PlayIdleAnimation(npcSpriteRenderer, playerIdleFrames));

        RefreshBoard();
        RefreshHud(val != 0
            ? "NPC đi qua ô +" + val + "! Score: " + currentScore
            : "NPC di chuyển. Score: " + currentScore);

        // Kiểm tra thắng
        if (npcPosition.y == Rows - 1)
            EndGame();
    }

    private IEnumerator PlayAiTurn()
    {
        RefreshHud("AI đang suy nghĩ...");
        yield return new WaitForSeconds(0.5f);

        PlaceStrategicAiBlock();
        RefreshBoard();

        yield return new WaitForSeconds(0.3f);

        RefreshHud("AI đã đặt chặn. NPC đang di chuyển...");

        // NPC di chuyển 1 bước sau lượt AI
        yield return StartCoroutine(MoveNPCOneStep());
    }

    // ================================================================
    //                      AI STRATEGY
    // ================================================================

    private void PlaceStrategicAiBlock()
    {
        if (aiBlocksPlaced >= MaxBlocks) return;

        // Tính đường đi hiện tại & projected score
        List<Vector2Int> currentPath = FindPath(npcPosition);
        if (currentPath == null) return;

        int currentProjected = currentScore + CalculatePathScore(currentPath);
        int aiDesiredParity = (aiRole == Role.Angel) ? 0 : 1; // 0 = chẵn, 1 = lẻ

        // Nếu projected parity đã đúng ý AI và AI còn >= 3 block → tiết kiệm
        if (currentProjected % 2 == aiDesiredParity && aiBlocksPlaced <= MaxBlocks - 3)
            return;

        float bestScore = float.MinValue;
        Vector2Int bestPosition = new Vector2Int(-1, -1);

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (IsWall(candidate) || candidate == npcPosition || y == Rows - 1) continue;

                aiWalls[x, y] = true;
                if (!IsBoardLayoutLegal())
                {
                    aiWalls[x, y] = false;
                    continue;
                }

                List<Vector2Int> newPath = FindPath(npcPosition);
                if (newPath == null)
                {
                    aiWalls[x, y] = false;
                    continue;
                }

                int newProjected = currentScore + CalculatePathScore(newPath);
                int newParity = Mathf.Abs(newProjected) % 2;

                float candidateScore;
                if (newParity == aiDesiredParity)
                {
                    // Parity đúng ý AI — ưu tiên cao
                    // Thêm bonus cho path dài hơn (khó cho player sửa)
                    candidateScore = 100f + (newPath.Count - currentPath.Count) * 0.5f;
                    // Thêm bonus cho sự khác biệt lớn (ổn định hơn)
                    candidateScore += Mathf.Abs(newProjected - currentProjected) * 0.3f;
                }
                else
                {
                    // Parity sai — nhưng vẫn xếp hạng (fallback)
                    candidateScore = -10f + (newPath.Count - currentPath.Count) * 0.1f;
                }

                if (candidateScore > bestScore)
                {
                    bestScore = candidateScore;
                    bestPosition = candidate;
                }

                aiWalls[x, y] = false;
            }
        }

        // Chỉ đặt nếu tìm được vị trí có lợi
        if (bestPosition.x >= 0 && bestScore > 0f)
        {
            aiWalls[bestPosition.x, bestPosition.y] = true;
            aiBlocksPlaced++;
        }
    }

    private int CalculatePathScore(List<Vector2Int> path)
    {
        int sum = 0;
        for (int i = 1; i < path.Count; i++)
        {
            int x = path[i].x, y = path[i].y;
            if (y == Rows - 1) continue; // Hàng đích không có giá trị

            // AI không biết giá trị ô secret chưa reveal
            if (cellIsSecret[x, y] && !cellRevealed[x, y]) continue;

            sum += cellValues[x, y];
        }
        return sum;
    }

    // ================================================================
    //                      PATHFINDING (A*)
    // ================================================================

    private List<Vector2Int> FindPath(Vector2Int start)
    {
        List<Vector2Int> openSet = new List<Vector2Int> { start };
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        while (openSet.Count > 0)
        {
            int bestIndex = 0;
            int bestF = GetScore(gScore, openSet[0]) + Heuristic(openSet[0]);
            for (int i = 1; i < openSet.Count; i++)
            {
                int score = GetScore(gScore, openSet[i]) + Heuristic(openSet[i]);
                if (score < bestF || (score == bestF && Heuristic(openSet[i]) < Heuristic(openSet[bestIndex])))
                {
                    bestF = score;
                    bestIndex = i;
                }
            }

            Vector2Int current = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);
            if (current.y == Rows - 1)
                return ReconstructPath(cameFrom, current, start);

            closedSet.Add(current);
            foreach (Vector2Int direction in GetNeighborOffsets(current))
            {
                Vector2Int neighbor = current + direction;
                if (!IsInside(neighbor) || IsWall(neighbor) || closedSet.Contains(neighbor))
                    continue;

                int tentativeScore = GetScore(gScore, current) + 1;
                if (tentativeScore >= GetScore(gScore, neighbor)) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeScore;
                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int goal, Vector2Int start)
    {
        List<Vector2Int> path = new List<Vector2Int> { goal };
        Vector2Int current = goal;
        while (current != start)
        {
            if (!cameFrom.TryGetValue(current, out Vector2Int previous)) return null;
            current = previous;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private int GetScore(Dictionary<Vector2Int, int> scores, Vector2Int position)
    {
        return scores.TryGetValue(position, out int score) ? score : int.MaxValue / 4;
    }

    private int Heuristic(Vector2Int position)
    {
        return Rows - 1 - position.y;
    }

    // ================================================================
    //                    BOARD VALIDATION
    // ================================================================

    private bool IsBoardLayoutLegal()
    {
        if (FindPath(npcPosition) == null) return false;
        return !HasEnclosedArea();
    }

    private bool HasEnclosedArea()
    {
        bool[,] reachedFromEdge = new bool[Columns, Rows];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < Columns; x++)
        {
            AddEdgeCell(new Vector2Int(x, 0), reachedFromEdge, queue);
            AddEdgeCell(new Vector2Int(x, Rows - 1), reachedFromEdge, queue);
        }
        for (int y = 0; y < Rows; y++)
        {
            AddEdgeCell(new Vector2Int(0, y), reachedFromEdge, queue);
            AddEdgeCell(new Vector2Int(Columns - 1, y), reachedFromEdge, queue);
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in GetNeighborOffsets(current))
            {
                Vector2Int neighbor = current + direction;
                if (!IsInside(neighbor) || IsWall(neighbor) || reachedFromEdge[neighbor.x, neighbor.y])
                    continue;
                reachedFromEdge[neighbor.x, neighbor.y] = true;
                queue.Enqueue(neighbor);
            }
        }

        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
                if (!IsWall(new Vector2Int(x, y)) && !reachedFromEdge[x, y])
                    return true;

        return false;
    }

    private void AddEdgeCell(Vector2Int position, bool[,] reached, Queue<Vector2Int> queue)
    {
        if (IsWall(position) || reached[position.x, position.y]) return;
        reached[position.x, position.y] = true;
        queue.Enqueue(position);
    }

    // ================================================================
    //                     WIN CONDITION
    // ================================================================

    private void EndGame()
    {
        isGameOver = true;
        isPlayerTurn = false;
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }

        bool scoreIsEven = (currentScore % 2 == 0);
        Role winner = scoreIsEven ? Role.Angel : Role.Evil;
        bool playerWon = (winner == playerRole);

        string scoreInfo = "Score cuối: " + currentScore +
            (scoreIsEven ? " (Chẵn → Angel thắng)" : " (Lẻ → Evil thắng)");
        RefreshHud(scoreInfo);
        ShowEndOverlay(playerWon, winner);
    }

    private void ShowEndOverlay(bool playerWon, Role winnerRole)
    {
        GameObject canvasObj = GameObject.Find("GameCanvas");
        if (canvasObj == null) return;

        GameObject overlay = new GameObject("GameOverOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.sprite = squareSprite;
        overlayImage.color = new Color(0.015f, 0.025f, 0.04f, 0.85f);
        endOverlay = overlay;

        GameObject card = CreatePanel("ResultCard", overlay.transform, new Color(0.07f, 0.1f, 0.14f, 1f));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 380f);
        cardRect.anchoredPosition = Vector2.zero;

        Color resultColor = playerWon
            ? new Color(0.36f, 0.82f, 0.64f)
            : new Color(1f, 0.38f, 0.4f);

        CreateText(card.transform, playerWon ? "VICTORY!" : "DEFEAT!", 48f,
            new Vector2(0f, -35f), new Vector2(480f, 70f),
            resultColor, TextAlignmentOptions.Center, FontStyles.Bold);

        string winnerName = winnerRole == Role.Angel ? "ANGEL" : "EVIL";
        CreateText(card.transform, winnerName + " thắng!", 26f,
            new Vector2(0f, -105f), new Vector2(480f, 40f),
            winnerRole == Role.Angel ? new Color(0.4f, 0.75f, 1f) : new Color(1f, 0.45f, 0.4f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        CreateText(card.transform, "Score cuối: " + currentScore +
            (currentScore % 2 == 0 ? "  (Chẵn)" : "  (Lẻ)"), 20f,
            new Vector2(0f, -155f), new Vector2(480f, 35f),
            new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, FontStyles.Normal);

        CreateText(card.transform,
            playerWon ? "Bạn đã thao túng đường đi thành công!" : "AI đã hướng NPC đi theo ý nó.",
            17f, new Vector2(0f, -200f), new Vector2(480f, 35f),
            new Color(0.7f, 0.78f, 0.86f), TextAlignmentOptions.Center, FontStyles.Normal);

        Button restart = CreateButton(card.transform, "RestartButton", "CHƠI LẠI",
            new Vector2(0f, -270f), out Image restartImage, out _);
        restartImage.color = new Color(0.12f, 0.43f, 0.72f);
        restart.onClick.AddListener(RestartGame);
    }

    private void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ================================================================
    //                        HELPERS
    // ================================================================

    private bool IsInside(Vector2Int position)
    {
        return position.x >= 0 && position.x < Columns && position.y >= 0 && position.y < Rows;
    }

    private Vector2Int ClampToBoard(Vector2Int position)
    {
        return new Vector2Int(
            Mathf.Clamp(position.x, 0, Columns - 1),
            Mathf.Clamp(position.y, 0, Rows - 1));
    }

    private bool IsWall(Vector2Int position)
    {
        if (!IsInside(position)) return true;
        return fixedWalls[position.x, position.y] ||
               playerWalls[position.x, position.y] ||
               aiWalls[position.x, position.y];
    }

    // ========================= LEGACY PUBLIC STUBS =========================
    // Giữ để tương thích nếu có button/event reference trong scene
    public void SetModeStart() { }
    public void SetModeEnd() { }
    public void SetModeObstacle() { }
    public void CalculateAStar() { }
    public void NextStep() { }
    public void PrevStep() { }
    public void ResetAll() => RestartGame();
}
