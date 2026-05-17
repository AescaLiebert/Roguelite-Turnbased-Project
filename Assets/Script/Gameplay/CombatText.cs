using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatText : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI DamageOnChartext;

    [SerializeField] private float lifeTime;

    [SerializeField] private float speed;

    // Start is called before the first frame update
    void Start()
    {
       StartCoroutine(FadeOut());
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }

    private void Move()
    {
        //transform.Translate(Vector3.up * speed * Time.deltaTime);
       transform.Translate(Vector3.up * speed * Time.deltaTime);
    }

    public IEnumerator FadeOut()
    {
        //transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
        
        
        float startAlpha = DamageOnChartext.color.a;

        float rate = 0.25f / lifeTime;

        float progress = 0f;

        while (progress < 2.0f)
        {
            Color tmp = DamageOnChartext.color;

            tmp.a = Mathf.Lerp(startAlpha, 0f, progress);

            DamageOnChartext.color = tmp;

            progress += Time.deltaTime;
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
}
