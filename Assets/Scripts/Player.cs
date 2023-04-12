using System.Collections;
using System.Collections.Generic;
using Momat.Runtime;
using UnityEngine;

public class Player : MonoBehaviour
{
    private MomatAnimator momatAnimator;
    // Start is called before the first frame update
    void Start()
    {
        momatAnimator = GetComponent<MomatAnimator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
