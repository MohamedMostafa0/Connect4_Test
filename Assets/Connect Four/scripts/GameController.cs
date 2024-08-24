using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine.Events;

public struct WinMessage : NetworkMessage
{
	public int winner;
}

namespace ConnectFour
{
	public class GameController : MonoBehaviour 
	{
		public static GameController Instance;

		enum Piece
		{
			Empty = 0,
			Blue = 1,
			Red = 2
		}

		[Range(3, 8)]
		public int numRows = 6;
		[Range(3, 8)]
		public int numColumns = 7;

		[Tooltip("How many pieces have to be connected to win.")]
		public int numPiecesToWin = 4;

		[Tooltip("Allow diagonally connected Pieces?")]
		public bool allowDiagonally = true;
		
		public float dropTime = 4f;

		// Gameobjects 
		public GameObject pieceRed;
		public GameObject pieceBlue;
		public GameObject pieceField;
        public GameObject pieceBlueTemp;
        public GameObject pieceRedTemp;

        public GameObject winningText;
		public string playerWonText = "You Won!";
		public string playerLoseText = "You Lose!";
		public string drawText = "Draw!";

		public GameObject gameObjectField;

		/// <summary>
		/// The Game field.
		/// 0 = Empty
		/// 1 = Blue
		/// 2 = Red
		/// </summary>
		int[,] field;

		bool gameOver = false;

        private void Awake()
        {
			Instance = this;
        }

		public List<NetworkRoomPlayer> players;

        // Use this for initialization
        void Start () 
		{
			NetworkClient.RegisterHandler<WinMessage>(ShowWinner);
			int max = Mathf.Max (numRows, numColumns);

			if(numPiecesToWin > max)
				numPiecesToWin = max;

			CreateField ();
        }

		/// <summary>
		/// Creates the field.
		/// </summary>
		void CreateField()
		{
			winningText.SetActive(false);

			gameObjectField = GameObject.Find ("Field");
			if(gameObjectField != null)
			{
				DestroyImmediate(gameObjectField);
			}
			gameObjectField = new GameObject("Field");

			// create an empty field and instantiate the cells
			field = new int[numColumns, numRows];
			for(int x = 0; x < numColumns; x++)
			{
				for(int y = 0; y < numRows; y++)
				{
					field[x, y] = (int)Piece.Empty;
					GameObject g = Instantiate(pieceField, new Vector3(x, y * -1, -1), Quaternion.identity) as GameObject;
					g.transform.parent = gameObjectField.transform;
				}
			}

			gameOver = false;

			// center camera
			Camera.main.transform.position = new Vector3(
				(numColumns-1) / 2.0f, -((numRows-1) / 2.0f), Camera.main.transform.position.z);

			winningText.transform.position = new Vector3(
				(numColumns-1) / 2.0f, -((numRows-1) / 2.0f) + 1, winningText.transform.position.z);
		}

		/// <summary>
		/// Spawns a piece at mouse position above the first row
		/// </summary>
		/// <returns>The piece.</returns>
		[ServerCallback]
		public void SpawnPiece(int turn)
		{
			if (gameOver) return;
			Vector3 spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

			GameObject g = Instantiate(
					turn == 0 ? pieceBlue : pieceRed,
					new Vector3(
					Mathf.Clamp(spawnPos.x, 0, numColumns-1), 
					gameObjectField.transform.position.y + 1, 0),
					Quaternion.identity);

            NetworkServer.Spawn(g, players[turn].connectionToClient);
		}
        [ServerCallback]
        public GameObject SpawnPieceTemp(int turn, Vector3 pos)
        {
            GameObject g = Instantiate(
                    turn == 1 ? pieceBlueTemp : pieceRedTemp, pos, Quaternion.identity);

            NetworkServer.Spawn(g);
            return g;
        }
        [ServerCallback]
		public void Despawn(GameObject obj)
		{
			NetworkServer.Destroy(obj);
		}

		public void DropPiece(GameObject obj, int turn)
		{
			StartCoroutine(dropPiece(obj, turn));
		}

		public bool FindFreeCell(GameObject obj, int turn, out Vector3 startPos, out Vector3 endPos)
		{
            startPos = obj.transform.position;
            endPos = new Vector3();

            // round to a grid cell
            int x = Mathf.RoundToInt(startPos.x);
            startPos = new Vector3(x, startPos.y, startPos.z);

            // is there a free cell in the selected column?
            bool foundFreeCell = false;
            for (int i = numRows - 1; i >= 0; i--)
            {
                if (field[x, i] == 0)
                {
                    foundFreeCell = true;
                    field[x, i] = turn == 0 ? (int)Piece.Blue : (int)Piece.Red;
                    endPos = new Vector3(x, i * -1, startPos.z);

                    break;
                }
            }
			return foundFreeCell;
        }
		/// <summary>
		/// This method searches for a empty cell and lets 
		/// the object fall down into this cell
		/// </summary>
		/// <param name="gObject">Game Object.</param>
		IEnumerator dropPiece(GameObject gObject, int turn)
		{
			bool foundFreeCell = FindFreeCell(gObject, turn, out Vector3 startPosition, out Vector3 endPosition);

            if (foundFreeCell)
			{

				float distance = Vector3.Distance(startPosition, endPosition);

				float t = 0;
				while(t < 1)
				{
					t += Time.deltaTime * dropTime * ((numRows - distance) + 1);

					gObject.transform.position = Vector3.Lerp (startPosition, endPosition, t);
					yield return null;
				}

				SpawnPieceTemp(turn, endPosition);
				Despawn(gObject);

                // run coroutine to check if someone has won
                int won = Won(turn);
				if(won >= 0)
				{
					NetworkServer.SendToAll(new WinMessage
					{
						winner = won,
					});
                }
                SpawnPiece(turn);
			}


			yield return 0;
		}

		int Won(int turn)
		{
			int check = CheckForWin();
			if(check != 0)
			{
				gameOver = true;
			}
			// if Game Over update the winning text to show who has won
			if(gameOver == true)
			{
				return turn;
            }
            else 
			{
				// check if there are any empty cells left, if not set game over and update text to show a draw
				if(!FieldContainsEmptyCell())
				{
					gameOver = true;
					return 2;
                }
            }
			return -1;
		}

		[ClientCallback]
		public void ShowWinner(WinMessage state)
		{
			string str = state.winner == 2 ? drawText : state.winner == 1 ? "Winner Player 1" : "Winner Player 2";
			winningText.SetActive(true);
            winningText.GetComponent<TextMesh>().text = str;
		}

		private int CheckForWin()
		{
			int i, j;


			//checks horizontal win
			for (i = 0; i < numColumns; i++)
				for (j = 0; j < numRows - 3; j++)
					if (field[i, j] != 0 && field[i, j] == field[i, j + 1] && field[i, j] == field[i, j + 2] && field[i, j] == field[i, j + 3])
						return field[i, j];
			//return 1;

			//checks vertical win
			for (i = 0; i < numColumns - 3; i++)
				for (j = 0; j < numRows; j++)
					if (field[i, j] != 0 && field[i, j] == field[i + 1, j] && field[i, j] == field[i + 2, j] && field[i, j] == field[i + 3, j])
						return field[i, j];
			//return 2;

			//checks rigth diagonal win
			for (i = 0; i < numColumns - 3; i++)
				for (j = 0; j < numRows - 3; j++)
					if (field[i, j] != 0 && field[i, j] == field[i + 1, j + 1] && field[i, j] == field[i + 2, j + 2] && field[i, j] == field[i + 3, j + 3])
						return field[i, j];


			//checks left diagonal win
			for (i = 0; i < numColumns - 3; i++)
				for (j = 0; j < numRows - 3; j++)
					if (field[i, j] != 0 && field[i, j] == field[i + 1, j - 1] && field[i, j] == field[i + 2, j - 2] && field[i, j] == field[i + 3, j - 3])
						return field[i, j];

			return 0;
		}

		/// <summary>
		/// check if the field contains an empty cell
		/// </summary>
		/// <returns><c>true</c>, if it contains empty cell, <c>false</c> otherwise.</returns>
		bool FieldContainsEmptyCell()
		{
			for(int x = 0; x < numColumns; x++)
			{
				for(int y = 0; y < numRows; y++)
				{
					if(field[x, y] == (int)Piece.Empty)
						return true;
				}
			}
			return false;
		}
	}
}
