using UnityEngine;
using UnityEngine.CrashReportHandler;
using UnityEngine.SceneManagement;

namespace Vesolovsky.Core.Services
{
    //TODO: add to the core
    public class CrashReportMetaDataSetter : MonoBehaviour
    {
        private void Awake()
        {
            CrashReportHandler.SetUserMetadata("build_version", BuildVersion.CURRENT_VERSION);
            CrashReportHandler.SetUserMetadata("platform", Application.platform.ToString());
            CrashReportHandler.SetUserMetadata("scene", SceneManager.GetActiveScene().name);
        }
    }
}
