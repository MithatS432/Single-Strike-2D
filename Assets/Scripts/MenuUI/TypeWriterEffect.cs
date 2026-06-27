using System.Collections;
using UnityEngine;
using TMPro;

public class TypeWriterEffect : MonoBehaviour
{
    public TextMeshProUGUI descriptionText;
    public float letterDelay = 0.03f;

    private Coroutine typingCoroutine;
    private string originalText;

    void Awake()
    {
        originalText = descriptionText.text;
    }

    public void RestartTyping()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText());
    }

    public void ResetText()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        descriptionText.text = originalText;
    }

    IEnumerator TypeText()
    {
        descriptionText.text = "";

        for (int i = 0; i < originalText.Length; i++)
        {
            descriptionText.text += originalText[i];
            yield return new WaitForSeconds(letterDelay);
        }
    }
}