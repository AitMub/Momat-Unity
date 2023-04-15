using System.Collections;
using System.Collections.Generic;
using Momat.Runtime;
using Unity.Mathematics;
using UnityEngine;

public class Player : MonoBehaviour
{
    private MomatAnimator momatAnimator;
    // Start is called before the first frame update
    void Start()
    {
        Quaternion rotation = Quaternion.AngleAxis(90f, Vector3.up);
        Vector3 translate = new Vector3(1f, 0f, -1f);
        AffineTransform mat = new AffineTransform(translate, rotation);

        Quaternion rotationA = Quaternion.AngleAxis(-90f, Vector3.up);
        AffineTransform matA = new AffineTransform(new Vector3(0, 0, 2), rotationA);
        AffineTransform matB = new AffineTransform(new Vector3(1, 0, 3), Quaternion.identity);
        AffineTransform diff = matA.inverse() * matB;

        Quaternion rotationW = Quaternion.AngleAxis(90, Vector3.up);
        AffineTransform matWorld = new AffineTransform(new Vector3(3, 0, 0), rotationW);
        AffineTransform matWB1 = diff * matWorld;
        AffineTransform matWB2 = matWorld * diff;

        Quaternion qPrev = new Quaternion(-2.310494E-08f, -0.6921239f, 8.061203E-08f, 0.7205052f);
        Quaternion qCurr = new Quaternion(-2.819978E-08f, -0.7428225f, 8.515291E-08f, 0.6705722f);
        Quaternion qDiff = Quaternion.Inverse(qPrev) * qCurr;
        Debug.Log($"prev euler {qPrev.eulerAngles}\n curr euler {qCurr.eulerAngles}\n diff euler {qDiff.eulerAngles}");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
