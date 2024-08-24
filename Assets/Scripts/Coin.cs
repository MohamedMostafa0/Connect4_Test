using ConnectFour;
using Mirror;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    private bool isMousePressed;
    public int id;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    private void Update()
    {
        if (!isOwned) return;
        if (isMousePressed) return;

        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(
            Mathf.Clamp(pos.x, 0, GameController.Instance.numColumns - 1),
            GameController.Instance.gameObjectField.transform.position.y + 1, 0);

        if (Input.GetMouseButtonDown(0))
        {
            isMousePressed = true;
            GetNewCoin();
            //StartCoroutine(dropPiece(gameObjectTurn));
        }
    }
    [Command]
    public void GetNewCoin()
    {
        int t = id;
        t++;
        if (t >= 2) t = 0;
        GameController.Instance.DropPiece(gameObject, t);
    }
}
