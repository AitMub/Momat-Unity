using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraScript : MonoBehaviour
{
    public Transform followTransform;
    public Vector3 offset;
    
    // Start is called before the first frame update
    void Start()
    {
        offset = followTransform.position - transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = followTransform.position - offset;
    }
}
