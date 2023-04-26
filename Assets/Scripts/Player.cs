using System;
using System.Collections;
using System.Collections.Generic;
using Momat.Runtime;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Player : MonoBehaviour
{
    private MomatAnimator momatAnimator;

    private Vector3 desiredLocalDirection;
    
    [SerializeField] private float acceleration = 2.0f;
    [SerializeField] private float runSpeed = 2.0f;
    [SerializeField] private float walkSpeed = 1.0f;
    private float currSpeed;

    private RuntimeTrajectory futureTrajectory;

    public Transform hand;
    
    // Start is called before the first frame update
    private void Start()
    {
        momatAnimator = GetComponent<MomatAnimator>();
        futureTrajectory = new RuntimeTrajectory();
        futureTrajectory.trajectoryData.AddLast(new TrajectoryPoint(transform, 1.0f));
        futureTrajectory.trajectoryData.AddLast(new TrajectoryPoint(transform, 0.5f));
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateDesiredDirection();
        UpdateFutureTrajectory();
        momatAnimator.SetFutureLocalTrajectory(futureTrajectory);
    }

    private void UpdateDesiredDirection()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currSpeed = Mathf.Lerp(currSpeed, runSpeed, Time.deltaTime * acceleration);
        }
        else
        {
            currSpeed = Mathf.Lerp(currSpeed, walkSpeed, Time.deltaTime * acceleration);
        }
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        desiredLocalDirection = new Vector3(x, 0, z) * currSpeed;
        desiredLocalDirection = transform.worldToLocalMatrix * new Vector4
            (desiredLocalDirection.x, desiredLocalDirection.y, desiredLocalDirection.z, 0f);
    }

    private void UpdateFutureTrajectory()
    {
        foreach (var tp in futureTrajectory.trajectoryData)
        {
            tp.transform = new AffineTransform
                ((desiredLocalDirection * tp.timeStamp) , quaternion.identity);
        }
    }
}
