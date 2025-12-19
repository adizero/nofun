PROJECT_ROOT="${PWD}"

"$HOME/Unity/Hub/Editor/2023.3.0a18/Editor/Unity" \
    -batchmode -nographics -quit \
    -projectPath "${PROJECT_ROOT}" \
    -executeMethod Nofun.CommandLineBuild.BuildAndroidApk \
    -customBuildPath "${PROJECT_ROOT}/Builds/Android/app.apk" \
    -logFile "${PROJECT_ROOT}/build-android.log"
