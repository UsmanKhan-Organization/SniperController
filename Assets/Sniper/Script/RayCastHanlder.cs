using UnityEngine;

public class RayCastHanlder : MonoBehaviour
{
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private LayerMask detectionLayers = ~0;

    public bool HasHit;
    public RaycastHit Hit { get; private set; }

    public void CheckHit()
    {
        HasHit = Physics.Raycast(
            transform.position,
            transform.forward,
            out RaycastHit hit,
            rayDistance,
            detectionLayers,
            QueryTriggerInteraction.Ignore);

        if (HasHit)
            Hit = hit;
    }
}
