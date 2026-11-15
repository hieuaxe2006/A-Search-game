using UnityEngine;
using TMPro;

public class HexNode : MonoBehaviour
{
    public int q, r; // Tọa độ Axial
    public bool isObstacle;
    public HexNode parent;

    public int gCost, hCost;
    public int fCost => gCost + hCost;

    public SpriteRenderer spriteRenderer;
    // Đổi từ TextMeshPro thành TextMeshProUGUI
    public TextMeshProUGUI textF, textG, textH, textLabel;

    private Color defaultColor = new Color(0.1f, 0.5f, 0.8f); // Xanh lam mặc định

    public void Init(int q, int r)
    {
        this.q = q;
        this.r = r;
        ResetNode();
    }

    public void SetState(string state)
    {
        switch (state)
        {
            case "Start":
                spriteRenderer.color = Color.red;
                textLabel.text = "Start";
                isObstacle = false;
                break;
            case "End":
                spriteRenderer.color = new Color(0, 0.5f, 1f); // Xanh dương đậm
                textLabel.text = "End";
                isObstacle = false;
                break;
            case "Obstacle":
                spriteRenderer.color = Color.gray;
                textLabel.text = "Wall";
                isObstacle = true;
                break;
            case "Path":
                spriteRenderer.color = Color.yellow;
                break;
            // ... Các case cũ giữ nguyên (Start, End, Obstacle, Path)...
            case "Open":
                spriteRenderer.color = new Color(0.5f, 0.9f, 0.5f); // Xanh lá cây nhạt (Đang chờ xét)
                break;
            case "Closed":
                spriteRenderer.color = new Color(0.6f, 0.4f, 0.6f); // Tím nhạt (Đã duyệt xong)
                break;
        }
    }

    public void ShowCosts()
    {
        if (!isObstacle && textLabel.text != "Start" && textLabel.text != "End")
        {
            // Nhân 10 giả lập chi phí di chuyển như demo
            textG.text = (gCost / 10f).ToString("0.0");
            textH.text = (hCost / 10f).ToString("0.0");
            textF.text = (fCost / 10f).ToString("0.0");
        }
    }

    public void ResetNode()
    {
        spriteRenderer.color = defaultColor;
        textLabel.text = "";
        textG.text = "";
        textH.text = "";
        textF.text = "";
        isObstacle = false;
        parent = null;
        gCost = 0;
        hCost = 0;
    }
}