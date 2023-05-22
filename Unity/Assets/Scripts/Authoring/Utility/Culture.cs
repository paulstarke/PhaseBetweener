using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.Threading;
public class Culture : MonoBehaviour
{
    void Awake()
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
    }
}
