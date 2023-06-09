using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Momat.Runtime;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class Player : MonoBehaviour
{
    private MomatAnimator momatAnimator;

    private Vector3 desiredLocalDirection;

    [SerializeField] private float acceleration = 2.0f;
    [SerializeField] private float runSpeed = 2.0f;
    [SerializeField] private float walkSpeed = 1.0f;
    private float currSpeed;

    private RuntimeTrajectory futureTrajectory;
    
    // Start is called before the first frame update
    private void Start()
    {
        momatAnimator = GetComponent<MomatAnimator>();
        // momatAnimator.SetCostComputeFunc(ComputeCost);
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
        if (x == 0 && z == 0)
        {
            desiredLocalDirection = Vector3.zero;
        }
        else
        {
            var xMagnitude = Mathf.Abs(x);
            var zMagnitude = Mathf.Abs(z);
            var xWeight =  xMagnitude / (xMagnitude + zMagnitude);
            var magnitude = xMagnitude * xWeight + zMagnitude * (1f - xWeight);
            
            desiredLocalDirection = new Vector3(x, 0, z).normalized * (magnitude * currSpeed);
        }

        desiredLocalDirection = transform.worldToLocalMatrix * new Vector4
            (desiredLocalDirection.x, desiredLocalDirection.y, desiredLocalDirection.z, 0f);

        if (Input.GetKeyDown(KeyCode.J))
        {
            momatAnimator.TryTriggerEvent(1);
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            momatAnimator.TryTriggerEvent(2);
        }
    }

    private void UpdateFutureTrajectory()
    {
        Quaternion q = desiredLocalDirection.magnitude == 0 ? 
            Quaternion.identity : 
            Quaternion.FromToRotation(Vector3.forward, desiredLocalDirection);
        
        foreach (var tp in futureTrajectory.trajectoryData)
        {
            tp.transform = new AffineTransform
                ((desiredLocalDirection * 2f * tp.timeStamp) , q);
        }
    }

    private float ComputeCost(in FeatureVector featureVector)
    {
        return 0;
    }
}
