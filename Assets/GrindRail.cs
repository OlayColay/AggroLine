using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathCreation;

[RequireComponent(typeof(PathCreator))]
public class GrindRail : MonoBehaviour
{
    public bool railLoops = false;

    [HideInInspector] public EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Stop;

    void Awake()
    {
        if (railLoops)
            endOfPathInstruction = EndOfPathInstruction.Loop;
    }
}
