using UnityEngine;
using UnityEngine.InputSystem;

namespace GearPotion
{
    /// <summary>탑다운 정사영 카메라: WASD/방향키 이동, 휠 줌, 휠 버튼 드래그.</summary>
    public class CameraRig : MonoBehaviour
    {
        Camera cam;
        float fitSize = 7f;
        Vector3 home;
        Vector3 dragOrigin;
        bool dragging;

        // 하단 빌드 독(약 16%)과 상단 바(약 6%)를 피해 그리드를 가운데에 둔다
        const float BottomUi = 0.17f, TopUi = 0.065f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        public void Fit(int width)
        {
            float aspect = Mathf.Max(1.2f, cam.aspect);
            float usable = 1f - BottomUi - TopUi;
            float needV = (FactoryGrid.Height + 1.6f) / usable / 2f;
            float needH = (width + 1.2f) / aspect / 2f;
            fitSize = Mathf.Max(needV, needH);
            cam.orthographicSize = fitSize;
            float gridCenterY = (FactoryGrid.Height - 1f) / 2f;
            float offset = (BottomUi - TopUi) * fitSize; // 화면 높이 2*size 기준
            home = new Vector3(width / 2f, gridCenterY - offset, -10f);
            transform.position = home;
        }

        void Update()
        {
            if (!GameManager.Instance || !GameManager.Instance.InGame || GameManager.Instance.Modal) return;
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.homeKey.wasPressedThisFrame) { transform.position = home; cam.orthographicSize = fitSize; }
            }
            var pos = transform.position + (Vector3)(move * cam.orthographicSize * 1.2f * dt);

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !HUD.PointerOverUI)
                {
                    var before = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (scroll > 0 ? 0.9f : 1.1f), 3.5f, fitSize * 1.4f);
                    var after = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    pos += before - after;
                }
                if (mouse.middleButton.wasPressedThisFrame) { dragging = true; dragOrigin = cam.ScreenToWorldPoint(mouse.position.ReadValue()); }
                if (mouse.middleButton.wasReleasedThisFrame) dragging = false;
                if (dragging) pos += dragOrigin - cam.ScreenToWorldPoint(mouse.position.ReadValue());
            }
            var grid = GameManager.Grid;
            pos.x = Mathf.Clamp(pos.x, -1f, grid.Width + 1f);
            pos.y = Mathf.Clamp(pos.y, -3f, FactoryGrid.Height + 1f);
            pos.z = -10f;
            transform.position = pos;
        }
    }
}
