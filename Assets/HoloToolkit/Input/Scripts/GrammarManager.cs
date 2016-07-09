// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Windows.Speech;

namespace HoloToolkit.Unity
{
    /// <summary>
    /// GrammarManager allows you to specify semantic matches and methods in the Unity
    /// Inspector, instead of registering them explicitly in code.
    /// This also includes a setting to either automatically start the
    /// grammar recognizer or allow your code to start it.
    ///
    /// IMPORTANT: Please make sure to add the microphone capability in your app, in Unity under
    /// Edit -> Project Settings -> Player -> Settings for Windows Store -> Publishing Settings -> Capabilities
    /// or in your Visual Studio Package.appxmanifest capabilities.
    /// </summary>
    public partial class GrammarManager : MonoBehaviour
    {



        public enum SupportedTypes { None = 0, Bool, String, Int32, Int64, UInt16, UInt32, UInt64, Float, Double, Decimal, DateTime }

        public static Dictionary<SupportedTypes, Type> TypeDictionary = new Dictionary<SupportedTypes, Type>() {
                                                                                        { SupportedTypes.None, typeof(object) }, { SupportedTypes.Bool, typeof(bool) }, { SupportedTypes.String, typeof(string) },
                                                                                        { SupportedTypes.Int32, typeof(int) }, { SupportedTypes.Int64, typeof(long) }, { SupportedTypes.UInt16, typeof(UInt16) },
                                                                                        { SupportedTypes.UInt32, typeof(UInt32) }, { SupportedTypes.UInt64, typeof(UInt64) },{ SupportedTypes.Float, typeof(float) }, { SupportedTypes.Double, typeof(double) },
                                                                                        { SupportedTypes.Decimal, typeof(Decimal) }, { SupportedTypes.DateTime, typeof(DateTime) }
                                                                                            };

        /// <summary>
        /// Extension method replacement?
        /// </summary>

        public static Dictionary<SupportedTypes, Func<string, object>> Parsing = new Dictionary<SupportedTypes,
            Func<string, object>>() {
                                                                                        { SupportedTypes.None,s => s},



 { SupportedTypes.Bool, s=>bool.Parse(s) }, { SupportedTypes.String, s=>s },
                                                                                        { SupportedTypes.Int32, s=>int.Parse(s) }, { SupportedTypes.Int64, s=>Int32.Parse(s) }, { SupportedTypes.UInt16, UInt16.Parse(s) },
                                                                                        { SupportedTypes.UInt32, s=>UInt32.Parse(s) }, { SupportedTypes.UInt64,s=> UInt64.Parse(s) },{ SupportedTypes.Float, s=>float.Parse(s) },
                                                                                        { SupportedTypes.Double, s=>double.Parse(s) },{ SupportedTypes.Decimal, s=>Decimal.Parse(s) }, { SupportedTypes.DateTime,s=>DateTime.Parse(s) }
                                                                                            };

        /// <summary>
        /// If you are not parsing out additional arguments from your grammar rather than a single Out string, then recommend using the KeywordManager instead. Assumes you are returning a semantic dictionary.
        /// </summary>
        [Tooltip("Set this to the primary semantic variable in your SRGS (Speech Recognition Grammar Specification) .grxml file. E.g. Out.action='identifier'")]
        public string PrimaryActionKey="action";

    
        [System.Serializable]
        public struct KeyAndType
        {
            [Tooltip("Semantic Keyword is the Out.keyword that will contain arguments provided in a user\'s utterance.")]
            public string SemanticKeyword;
            [Tooltip("The expected basic value or string type associated with the semantic key")]
            public SupportedTypes ExpectedType;
            [Tooltip("If false, assumes the first entry in the values array is the desired value, ignores any additional returned result")]
            public bool IsIEnumerableCollection;

            [Tooltip("Enter default values or leave blank")]
            public string[] Values;
           
        }

        [Tooltip("The Semantic argument and expected type of the 1 or more keyword values. Note: This is not necessarily the same as the speech keyword, but rather the Out.key that is returned from the Grammar.")]
        public KeyAndType[] KeyAndTypes;

       [System.Serializable]
        public struct SemanticKeyAndResponse
        {
            [Tooltip("The keyword to recognize.")]
            public string Keyword;
            [Tooltip("The KeyCode to recognize.")]
            public KeyCode KeyCode;
            [Tooltip("The UnityEvent to be invoked when the keyword is recognized.")]
            public UnityEvent Response;

            [Tooltip("Enter secondary keywords that should be present as arguments (ONLY Necessary if there is more than one Argument with the Same Type to disambiguate. Set in order of the Unity Event Invocation.)")]
            public string[] SemanticKeywordPrecendence;
        }
    
        public struct SemanticKeyAndSemanticMeaning
        {
            public KeyAndType KeyType;
            public SemanticMeaning Meaning; 
        }

        // This enumeration gives the manager two different ways to handle the recognizer. Both will
        // set up the recognizer and add all keywords. The first causes the recognizer to start
        // immediately. The second allows the recognizer to be manually started at a later time.
        public enum RecognizerStartBehavior { AutoStart, ManualStart };

        [Tooltip("An enumeration to set whether the recognizer should start on or off.")]
        public RecognizerStartBehavior RecognizerStart;

        [Tooltip("An array of recognized semantic key matches and UnityEvents, to be set in the Inspector. Keys are case-sensitive")]
        public SemanticKeyAndResponse[] KeysAndResponses;
      

        private PhraseRecognizer grammarRecognizer;

        [ContextMenuItem("Select .grxml file", "SelectGrammarFile")]
        [ContextMenuItem("Create Example Grammar .grxml file", "CreateExampleGrammarFile")]
        [Tooltip("Partial Path in the StreamingAssets\\  folder to the .grxml grammar file in use.")]
        public string grammarPath;


        private Dictionary<string, SemanticKeyAndResponse> responses;

        private Dictionary<string, KeyAndType> secondaryKeywordDictionary; 


        private void CreateExampleGrammarFile()
        {
            Debug.Log("Create example grammar file...");

        }

        private void SelectGrammarFile()
        {
            Debug.Log("Select grammar file...");

            string path = EditorUtility.OpenFilePanel("Select a grxml file in the StreamingAssets folder.", Application.streamingAssetsPath, "grxml");

            grammarPath=path.ToLower().Replace(Application.streamingAssetsPath, string.Empty);

        }


        void Start()
        {



            if (KeysAndResponses.Length > 0)
            {
                //    // Convert the struct array into a dictionary, with the keywords and the keys and the methods as the values.
                //    // This helps easily link the keyword recognized to the UnityEvent to be invoked.
                responses = KeysAndResponses.ToDictionary(k=>k.Keyword,k=>k);

                secondaryKeywordDictionary=KeyAndTypes.ToDictionary(k=>k.SemanticKeyword,k=>k);

                string filePath = System.IO.Path.Combine(Application.streamingAssetsPath, grammarPath);

                if(System.IO.File.Exists(filePath))
                {

               

            Debug.Log("Loading grammar path = " + filePath);

                grammarRecognizer = new GrammarRecognizer(filePath); //new KeywordRecognizer(responses.Keys.ToArray());
                
                grammarRecognizer.OnPhraseRecognized += PhraseRecognizer_OnPhraseRecognized;
          
                if (RecognizerStart == RecognizerStartBehavior.AutoStart)
                {
               
                    grammarRecognizer.Start();
                }

                 Debug.Log("Is grammar running = " + grammarRecognizer.IsRunning);
                }
                else
                {
                    Debug.LogWarningFormat("File path for grxml Grammar file not found: {0}", filePath);
                }
            }
            else
            {
                Debug.LogError("Must have at least one keyword and Semantic array specified in the Inspector on " + gameObject.name + ".");
            }


}



        void Update()
        {
            ProcessKeyBindings();
        }

        void OnDestroy()
        {
            if (grammarRecognizer != null)
            {
                //StopPhraseRecognizer();
                grammarRecognizer.OnPhraseRecognized -= PhraseRecognizer_OnPhraseRecognized;
                grammarRecognizer.Dispose();

                PhraseRecognitionSystem.Restart();
            }
        }

        private void ProcessKeyBindings()
        {
            foreach (var kvp in KeysAndResponses)
            {
                if (Input.GetKeyDown(kvp.KeyCode))
                {
                    kvp.Response.Invoke();
                    return;
                }
            }
        }

        private void PhraseRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
           

            Debug.LogFormat("Confidence={0},duration={1},startTime={2},text={3}", args.confidence, args.phraseDuration, args.phraseStartTime,args.text);

            Dictionary<string, SemanticKeyAndSemanticMeaning> argDictionary = new Dictionary<string, SemanticKeyAndSemanticMeaning>();

            string primaryActionKey = null;

            if (args.semanticMeanings != null)
            {
                
              

                for (int i = 0; i < args.semanticMeanings.Length; i++)
                {
                    var semantic = args.semanticMeanings[i];

                    Debug.LogFormat("semantic meaning key={0} ", semantic.key);

                    if(primaryActionKey == null && semantic.key == PrimaryActionKey && semantic.values.Length > 0)
                    {
                        primaryActionKey = semantic.values[0];

                    }
                    else if(secondaryKeywordDictionary.ContainsKey(semantic.key))
                    {

                        var parameter = secondaryKeywordDictionary[semantic.key];

                        SemanticKeyAndSemanticMeaning combineInfo = new SemanticKeyAndSemanticMeaning() { KeyType = parameter, Meaning = semantic };

                        

                        for (int k = 0; k < semantic.values.Length; k++)
                        {
                            var val = semantic.values[k];
                            Debug.LogFormat("semantic value={0} ", val);
                        }

                        argDictionary.Add(semantic.key, combineInfo);
                    }
                   
                    
                    

                }
            }


            if(!string.IsNullOrEmpty(primaryActionKey))
            {

                var meaning = argDictionary[PrimaryActionKey];


                SemanticKeyAndResponse keyResponse;

                // Check to make sure the recognized keyword exists in the methods dictionary, then invoke the corresponding method.
                if (responses.TryGetValue(primaryActionKey, out keyResponse))
                {
                    
                    int eventCount = keyResponse.Response.GetPersistentEventCount();

                    for(int z=0;z<eventCount; z++)
                    {
                        MonoBehaviour targetBehavior= keyResponse.Response.GetPersistentTarget(z) as MonoBehaviour;

                        var methodName = keyResponse.Response.GetPersistentMethodName(z);

                    

                        if(argDictionary.Count == 0)
                        {
                            targetBehavior.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);


                        }
                        else
                        {

                            if (argDictionary.Count == 1)
                            {
                                object singleArg;
                               
                                foreach(var key in argDictionary.Keys)
                                {
                                    var info = argDictionary[key];

                                    if(info.KeyType.IsIEnumerableCollection)
                                    {
                                        List<object> values = new List<object>();

                                        for (int i = 0; i < info.Meaning.values.Length; i++)
                                        {
                                            values.Add(Parsing[info.KeyType.ExpectedType](info.Meaning.values[i]));
                                            
                                        }

                                        singleArg = values;
                                    }
                                    else 
                                    {
                                        singleArg = Parsing[info.KeyType.ExpectedType](info.Meaning.values[0]);
                                    }
                                    

                                    targetBehavior.SendMessage(methodName, singleArg, SendMessageOptions.DontRequireReceiver);
                                }

                             

                            }
                            else
                            {

                               
                                List<Type> typeList = new List<Type>();
                                List<System.Object> objectList = new List<System.Object>();

                                if(keyResponse.SemanticKeywordPrecendence.Length >0)
                                {
                                    foreach(var key in keyResponse.SemanticKeywordPrecendence)
                                    {
                                        if(argDictionary.ContainsKey(key))
                                        {
                                            object singleArg;

                                            var info = argDictionary[key];

                                            if (info.KeyType.IsIEnumerableCollection)
                                            {
                                                List<object> values = new List<object>();

                                                for (int i = 0; i < info.Meaning.values.Length; i++)
                                                {
                                                    values.Add(Parsing[info.KeyType.ExpectedType](info.Meaning.values[i]));

                                                }

                                                singleArg = values;
                                            }
                                            else
                                            {
                                                singleArg = Parsing[info.KeyType.ExpectedType](info.Meaning.values[0]);
                                            }

                                            typeList.Add(singleArg.GetType());
                                            objectList.Add(singleArg);

                                        }
                                    }

                                }



                                System.Type[] typeArray = typeList.ToArray(); 
                                System.Object[] parameters = objectList.ToArray();


                                // get the method assigned in the editor and call it
                                System.Reflection.MethodInfo methodInfo = UnityEventBase.GetValidMethodInfo(targetBehavior, methodName, typeArray);

                                if (methodInfo != null)
                                {
                                    methodInfo.Invoke(targetBehavior.gameObject, parameters);
                                }
                                else
                                {
                                    Debug.LogWarningFormat("Did not find method {0} on {1}, check parameter configuration.", methodName, targetBehavior.name);
                                }


                            }


                        }                        

                      


                        


                     
                        
                    }


                

                }


                
                }
                

            }
          
        
       


        //private void CalculateMove(Dictionary<string, SemanticMeaning> meaningDictionary)
        //{

        //    var numArr = meaningDictionary["_value"].values;

        //    var unitArr = meaningDictionary["unit"].values;

        //    var directionArr = meaningDictionary["direction"].values;

        //    float moveMultiplier;

        //    if(numArr != null && numArr.Length==1)
        //    {
        //        if(!float.TryParse(numArr[0],out moveMultiplier))
        //        {
        //            moveMultiplier = 1f; 
        //        }
                
        //    }
        //    else
        //    {
        //        moveMultiplier = 1f;
        //    }

        //    float conversionUnit;

        //    if(unitArr != null && unitArr.Length == 1)
        //    {
        //        var unit = unitArr[0];
        //        if (!string.IsNullOrEmpty(unit))
        //        {
        //            switch (unit)
        //            {
        //                case "m":
        //                    {

        //                        conversionUnit = 1f;
        //                    }

        //                    break;
        //                case "cm":
        //                    {

        //                        conversionUnit = .01f;
        //                    }
        //                    break;
        //                case "mm":
        //                    {

        //                        conversionUnit = .001f;
        //                    }

        //                    break;
        //                case "inches":
        //                    {

        //                        conversionUnit = 0.0254f;
        //                    }

        //                    break;
        //                case "feet":
        //                    {

        //                        conversionUnit = .3048f;
        //                    }

        //                    break;
        //                case "yard":
        //                    {

        //                        conversionUnit = .9144f;
        //                    }

        //                    break;
        //                default:
        //                    conversionUnit = .01f;
        //                    break;
        //            }

        //        }
        //          else
        //        {
        //            conversionUnit = .01f;   //centimeters.
        //        } 
                        
        //      }
        //        else
        //        {
        //            conversionUnit = .01f;   //centimeters.

        //        }


        //    var direction = directionArr[0];

        //    switch (direction)
        //    {
        //        case "left":
        //           // commandContext.MoveLeft(moveMultiplier * conversionUnit);
        //            break;

        //        case "right":
        //           /// commandContext.MoveRight(moveMultiplier * conversionUnit);
        //            break;

        //        case "up":
        //           // commandContext.MoveUp(moveMultiplier * conversionUnit);
        //            break;

        //        case "down":
        //           // commandContext.MoveDown(moveMultiplier * conversionUnit);
        //            break;
                
        //    }
        //}

        /// <summary>
        /// Make sure the keyword recognizer is off, then start it.
        /// Otherwise, leave it alone because it's already in the desired state.
        /// </summary>
        public void StartGrammarRecognizer()
        {
            if (grammarRecognizer != null && !grammarRecognizer.IsRunning)
            {

            
                grammarRecognizer.Start();
            }
        }

        /// <summary>
        /// Make sure the keyword recognizer is on, then stop it.
        /// Otherwise, leave it alone because it's already in the desired state.
        /// </summary>
        public void StopGrammarRecognizer()
        {
            if (grammarRecognizer != null && grammarRecognizer.IsRunning)
            {
                grammarRecognizer.Stop();
            }
        }


       

    }
}