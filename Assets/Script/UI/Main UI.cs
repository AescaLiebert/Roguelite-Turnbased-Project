using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainUI : MonoBehaviour
{
    private Canvas canvas;
    public Canvas Canvas { get { return canvas; } }

    public static MainUI instance;

    private void Awake()
    {
        instance = this;
        canvas = GetComponent<Canvas>();
    }

    public Vector3 ScalePosition(Vector3 pos)
    {
        Vector3 newPos;

        newPos = new Vector3(pos.x * canvas.transform.localScale.x
            , pos.y * canvas.transform.localScale.y
            , pos.z * canvas.transform.localScale.z);

        return newPos;
    }
}
