using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Core
{
    /// <summary>
    /// 테스트용 치트 키. 에디터와 개발 빌드(Development Build)에서만 동작하고, 출시 빌드에서는 스스로 꺼진다.
    /// 키는 Inspector의 액션 바인딩에서 바꿀 수 있다.
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private double grantAmount = 10000;
        [SerializeField] private InputAction grantAction = new InputAction("GrantCurrency", InputActionType.Button, "<Keyboard>/f1");

        private void Awake()
        {
            // Debug.isDebugBuild는 에디터와 개발 빌드에서만 true다.
            if (!Debug.isDebugBuild) enabled = false;
        }

        private void OnEnable()
        {
            grantAction.performed += OnGrant;
            grantAction.Enable();
        }

        private void OnDisable()
        {
            grantAction.performed -= OnGrant;
            grantAction.Disable();
        }

        private void OnGrant(InputAction.CallbackContext _)
        {
            game.GrantCookies(grantAmount);
            Debug.Log($"[DebugCheats] {game.Config.CurrencyName} +{NumberFormatter.Format(grantAmount)}");
        }
    }
}
