using UnityEngine;

namespace OniBrutal.Engine
{
    /// The behaviour Unity keeps where a GameObject's script class is not loaded ("missing
    /// script"). It holds the component's place, and it has no messages; component queries give
    /// null in its place (Components), as measured in the original player.
    internal sealed class MissingScript : MonoBehaviour
    {
    }
}
