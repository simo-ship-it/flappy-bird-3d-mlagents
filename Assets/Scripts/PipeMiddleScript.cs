using UnityEngine;
public class PipeMiddleScript : MonoBehaviour
{
    public PipesMoves pipe;
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BirdPlayerScript>() != null) pipe.Entered = true;
    }
}

