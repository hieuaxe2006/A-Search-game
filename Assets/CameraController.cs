using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Cài đặt Thu phóng (Zoom)")]
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 15f;

    [Header("Cài đặt Lướt (Pan)")]
    private Vector3 dragOrigin;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        // Lấy giá trị lăn chuột
        float scrollValue = Mouse.current.scroll.ReadValue().y;

        if (scrollValue != 0)
        {
            // Điều chỉnh orthographicSize của Camera 2D
            cam.orthographicSize -= scrollValue * zoomSpeed * 0.001f;

            // Giới hạn độ thu phóng không quá gần hoặc quá xa
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    private void HandlePan()
    {
        // Khi BẮT ĐẦU bấm chuột phải (hoặc có thể đổi thành middleButton cho chuột giữa)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            // Lưu lại vị trí chuột trong không gian World ngay lúc bắt đầu nhấn
            dragOrigin = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        }

        // Đang GIỮ chuột phải
        if (Mouse.current.rightButton.isPressed)
        {
            // Vị trí chuột hiện tại
            Vector3 currentPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            // Tính khoảng cách chênh lệch và dời vị trí Camera
            Vector3 difference = dragOrigin - currentPos;
            cam.transform.position += difference;
        }
    }
}