using System.Collections;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class TakingSidesScript : MonoBehaviour {

    public KMAudio sound;
    public KMBombInfo bomb;
    public KMNeedyModule needy;
    public KMSelectable[] buttons;
    public GameObject textObj;
    public GameObject rotator;
    public MeshFilter[] polygonRends;
    public Mesh[] polygons;

    private Coroutine rotateCo;
    int selectedPoly;
    int curDisp;
    bool isActive;
    bool bombSolved;

    static int moduleIdCounter = 1;
    int moduleId;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
        needy.OnNeedyActivation += OnNeedyActivation;
        needy.OnTimerExpired += OnTimerExpired;
        bomb.OnBombExploded += delegate () { OnEnd(false); };
        bomb.OnBombSolved += delegate () { OnEnd(true); };
    }

    void OnEnd(bool n)
    {
        bombSolved = true;
        if (n)
        {
            textObj.SetActive(false);
            rotator.transform.localScale = Vector3.zero;
        }
    }

    void Start()
    {
        textObj.SetActive(false);
        rotator.transform.localScale = Vector3.zero;
        Debug.LogFormat("[Taking Sides #{0}] Needy Taking Sides has loaded! Waiting for activation...", moduleId);
    }

    void PressButton(KMSelectable pressed)
    {
        if (isActive != false && bombSolved != true)
        {
            int index = Array.IndexOf(buttons, pressed);
            if (index == 0)
            {
                if (curDisp == 12)
                    return;
                sound.PlaySoundAtTransform("press", pressed.transform);
                pressed.AddInteractionPunch(.75f);
                curDisp++;
                textObj.GetComponent<TextMesh>().text = curDisp.ToString("00");
            }
            else if (index == 1)
            {
                if (curDisp == 3)
                    return;
                sound.PlaySoundAtTransform("press", pressed.transform);
                pressed.AddInteractionPunch(.75f);
                curDisp--;
                textObj.GetComponent<TextMesh>().text = curDisp.ToString("00");
            }
            else
            {
                pressed.AddInteractionPunch();
                if (curDisp - 3 == selectedPoly)
                {
                    sound.PlaySoundAtTransform("solve", transform);
                    Debug.LogFormat("[Taking Sides #{0}] Submission of {1} was correct! Generating new polygon...", moduleId, curDisp);
                    needy.SetNeedyTimeRemaining(Math.Min(needy.GetNeedyTimeRemaining() + 15, 80));
                    GeneratePoly();
                }
                else
                {
                    sound.PlaySoundAtTransform("strike", transform);
                    textObj.SetActive(false);
                    GetComponent<KMNeedyModule>().HandleStrike();
                    GetComponent<KMNeedyModule>().HandlePass();
                    rotator.transform.localScale = Vector3.zero;
                    isActive = false;
                    Debug.LogFormat("[Taking Sides #{0}] Submission of {1} was incorrect! Strike! Waiting for next activation...", moduleId, curDisp);
                }
            }
        }
    }

    void GeneratePoly()
    {
        selectedPoly = UnityEngine.Random.Range(0, polygons.Length);
        for (int i = 0; i < 2; i++)
            polygonRends[i].mesh = polygons[selectedPoly];
        Debug.LogFormat("[Taking Sides #{0}] The displayed polygon has {1} sides.", moduleId, selectedPoly + 3);
        if (rotateCo != null)
            StopCoroutine(rotateCo);
        rotateCo = StartCoroutine(HandleRotate());
    }

    protected void OnNeedyActivation()
    {
        textObj.SetActive(true);
        Debug.LogFormat("[Taking Sides #{0}] The module has activated!", moduleId);
        GeneratePoly();
        curDisp = 3;
        textObj.GetComponent<TextMesh>().text = curDisp.ToString("00");
        rotator.transform.localScale = Vector3.one;
        isActive = true;
    }

    protected void OnTimerExpired()
    {
        sound.PlaySoundAtTransform("strike", transform);
        textObj.SetActive(false);
        GetComponent<KMNeedyModule>().HandleStrike();
        rotator.transform.localScale = Vector3.zero;
        Debug.LogFormat("[Taking Sides #{0}] The timer ran out! Strike! Waiting for next activation...", moduleId);
        isActive = false;
    }

    IEnumerator HandleRotate()
    {
        float speed = UnityEngine.Random.Range(0.15f, 0.45f);
        int neg = UnityEngine.Random.Range(0, 2);
        while (true)
        {
            float t = 0f;
            int rotation = 0;
            while (rotation != 90)
            {
                while (t < 0.005f)
                {
                    yield return null;
                    t += Time.deltaTime * speed;
                }
                t = 0f;
                if (neg == 1)
                    rotator.transform.Rotate(0.0f, -0.5f, 0.0f, Space.Self);
                else
                    rotator.transform.Rotate(0.0f, 0.5f, 0.0f, Space.Self);
                rotation++;
            }
        }
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} submit <#> [Sets the display to the specified number and then submits]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify a number to submit!";
            else if (parameters.Length > 2)
                yield return "sendtochaterror Too many parameters!";
            else
            {
                int temp = -1;
                if (!int.TryParse(parameters[1], out temp))
                {
                    yield return "sendtochaterror!f The specified number '" + parameters[1] + "' is invalid!";
                    yield break;
                }
                if (temp < 3 || temp > 12)
                {
                    yield return "sendtochaterror The specified number '" + parameters[1] + "' is out of range 3-12!";
                    yield break;
                }
                yield return null;
                while (curDisp < temp)
                {
                    buttons[0].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                while (curDisp > temp)
                {
                    buttons[1].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                buttons[2].OnInteract();
            }
        }
    }

    void TwitchHandleForcedSolve()
    {
        //The code is done in a coroutine instead of here so that if the solvebomb command was executed this will just input the number right when it activates and it wont wait for its turn in the queue
        StartCoroutine(DealWithNeedy());
    }

    private IEnumerator DealWithNeedy()
    {
        while (!bombSolved)
        {
            while (!isActive) { yield return null; }
            if (needy.GetNeedyTimeRemaining() <= 65)
            {
                while (curDisp < selectedPoly + 3)
                {
                    buttons[0].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                while (curDisp > selectedPoly + 3)
                {
                    buttons[1].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                buttons[2].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
            yield return null;
        }
    }
}