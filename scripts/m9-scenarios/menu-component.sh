# Measurement: the menu and options, with the serialized fields of one component logged at frame
# 600 ($COMPONENT: GameObject path|component type).
FRAMES=640
DUMPS="600"
ARGS=(--input 420:click:891,531 --dump-component "600:${COMPONENT:-SceneInitializerFE/FrontEndManager/MainMenu/OptionsMenu/CenterContent/Content/Buttons/AudioButton/Text|LocText}")
