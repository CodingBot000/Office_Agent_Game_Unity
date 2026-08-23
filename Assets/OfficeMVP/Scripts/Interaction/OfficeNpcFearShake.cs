using UnityEngine;

public sealed class OfficeNpcFearShake : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.035f;
    [SerializeField] private float frequency = 28f;

    private Vector3 baseLocalPosition;
    private bool shaking;
    private float phase;

    public bool IsShaking => shaking;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void SetEmotion(string emotion)
    {
        shaking = emotion == "afraid" || emotion == "shocked";
        if (!shaking)
        {
            transform.localPosition = baseLocalPosition;
        }
    }

    private void LateUpdate()
    {
        if (!shaking)
        {
            return;
        }

        var offset = Mathf.Sin((Time.unscaledTime * frequency) + phase) * amplitude;
        transform.localPosition = baseLocalPosition + new Vector3(offset, 0f, 0f);
    }
}
