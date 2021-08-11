using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    AggroLineController parent;
    Animator animator;

    void Awake()
    {
        parent = transform.parent.GetComponent<AggroLineController>();
        animator = GetComponent<Animator>();
    }

    void Turn180()
    {
        StartCoroutine(parent.Turn180());
    }
    void Turn180Cancel()
    {
        StopCoroutine(parent.Turn180());
        animator.SetBool("Turn", false);
    }
}
