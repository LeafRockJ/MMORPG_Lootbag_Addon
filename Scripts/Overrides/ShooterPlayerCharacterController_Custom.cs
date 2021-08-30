﻿using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerARPG
{
    public partial class ShooterPlayerCharacterController_Custom : BasePlayerCharacterController, IShooterWeaponController, IWeaponAbilityController, IAimAssistAvoidanceListener
    {

        public const byte PAUSE_FIRE_INPUT_FRAMES_AFTER_CONFIRM_BUILD = 3;

        public enum ControllerMode
        {
            Adventure,
            Combat,
        }

        public enum ExtraMoveActiveMode
        {
            None,
            Toggle,
            Hold
        }

        public enum EmptyAmmoAutoReload
        {
            ReloadImmediately,
            ReloadOnKeysReleased,
            DoNotReload,
        }

        [Header("Camera Controls Prefabs")]
        [SerializeField]
        private FollowCameraControls gameplayCameraPrefab;
        [SerializeField]
        private FollowCameraControls minimapCameraPrefab;

        [Header("Controller Settings")]
        [SerializeField]
        private ControllerMode mode;
        [SerializeField]
        protected EmptyAmmoAutoReload emptyAmmoAutoReload;
        [SerializeField]
        private bool canSwitchViewMode;
        [SerializeField]
        private ShooterControllerViewMode viewMode;
        [SerializeField]
        private ExtraMoveActiveMode sprintActiveMode;
        [SerializeField]
        private ExtraMoveActiveMode crouchActiveMode;
        [SerializeField]
        private ExtraMoveActiveMode crawlActiveMode;
        [SerializeField]
        private bool unToggleCrouchWhenJump;
        [SerializeField]
        private bool unToggleCrawlWhenJump;
        [SerializeField]
        private float findTargetRaycastDistance = 16f;
        [SerializeField]
        private bool showConfirmConstructionUI = false;
        [SerializeField]
        private bool clampBuildPositionByBuildDistance = false;
        [SerializeField]
        protected bool buildRotationSnap;
        [SerializeField]
        protected float buildRotateAngle = 45f;
        [SerializeField]
        protected float buildRotateSpeed = 200f;
        [SerializeField]
        private RectTransform crosshairRect;

        [Header("TPS Settings")]
        [SerializeField]
        private float tpsZoomDistance = 3f;
        [SerializeField]
        private float tpsMinZoomDistance = 3f;
        [SerializeField]
        private float tpsMaxZoomDistance = 3f;
        [SerializeField]
        private Vector3 tpsTargetOffset = new Vector3(0.75f, 1.25f, 0f);
        [SerializeField]
        private Vector3 tpsTargetOffsetWhileCrouching = new Vector3(0.75f, 0.75f, 0f);
        [SerializeField]
        private Vector3 tpsTargetOffsetWhileCrawling = new Vector3(0.75f, 0.5f, 0f);
        [SerializeField]
        private float tpsFov = 60f;
        [SerializeField]
        private float tpsNearClipPlane = 0.3f;
        [SerializeField]
        private float tpsFarClipPlane = 1000f;
        [SerializeField]
        private bool turnForwardWhileDoingAction = true;
        [SerializeField]
        private float stoppedPlayingAttackOrUseSkillAnimationDelay = 0.5f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeed = 0f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeedWhileSprinting = 0f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeedWhileCrouching = 0f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeedWileCrawling = 0f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeedWileSwimming = 0f;
        [SerializeField]
        [Tooltip("Use this to turn character smoothly, Set this <= 0 to turn immediately")]
        private float turnSpeedWileDoingAction = 0f;

        [Header("FPS Settings")]
        [SerializeField]
        private float fpsZoomDistance = 0f;
        [SerializeField]
        private Vector3 fpsTargetOffset = new Vector3(0f, 0f, 0f);
        [SerializeField]
        private float fpsFov = 40f;
        [SerializeField]
        private float fpsNearClipPlane = 0.01f;
        [SerializeField]
        private float fpsFarClipPlane = 1000f;

        [Header("Aim Assist Settings")]
        [SerializeField]
        private bool enableAimAssist = false;
        [SerializeField]
        private bool enableAimAssistX = false;
        [SerializeField]
        private bool enableAimAssistY = true;
        [SerializeField]
        private bool aimAssistOnFireOnly = true;
        [SerializeField]
        private float aimAssistRadius = 0.5f;
        [SerializeField]
        private float aimAssistXSpeed = 20f;
        [SerializeField]
        private float aimAssistYSpeed = 20f;
        [SerializeField]
        private bool aimAssistCharacter = true;
        [SerializeField]
        private bool aimAssistBuilding = false;
        [SerializeField]
        private bool aimAssistHarvestable = false;

        [Header("Recoil Settings")]
        [SerializeField]
        private float recoilRateWhileMoving = 1.5f;
        [SerializeField]
        private float recoilRateWhileSprinting = 2f;
        [SerializeField]
        private float recoilRateWhileCrouching = 0.5f;
        [SerializeField]
        private float recoilRateWhileCrawling = 0.5f;
        [SerializeField]
        private float recoilRateWhileSwimming = 0.5f;

        public bool IsBlockController { get; private set; }
        public FollowCameraControls CacheGameplayCameraControls { get; private set; }
        public FollowCameraControls CacheMinimapCameraControls { get; private set; }
        public Camera CacheGameplayCamera { get { return CacheGameplayCameraControls.CacheCamera; } }
        public Camera CacheMiniMapCamera { get { return CacheMinimapCameraControls.CacheCamera; } }
        public Transform CacheGameplayCameraTransform { get { return CacheGameplayCameraControls.CacheCameraTransform; } }
        public Transform CacheMiniMapCameraTransform { get { return CacheMinimapCameraControls.CacheCameraTransform; } }
        public Vector2 CurrentCrosshairSize { get; private set; }
        public CrosshairSetting CurrentCrosshairSetting { get; private set; }
        public BaseWeaponAbility WeaponAbility { get; private set; }
        public WeaponAbilityState WeaponAbilityState { get; private set; }

        public ControllerMode Mode
        {
            get
            {
                if (viewMode == ShooterControllerViewMode.Fps)
                {
                    // If view mode is fps, controls type must be combat
                    return ControllerMode.Combat;
                }
                return mode;
            }
        }

        public ShooterControllerViewMode ViewMode
        {
            get { return viewMode; }
            set { viewMode = value; }
        }

        public float CameraZoomDistance
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsZoomDistance : fpsZoomDistance; }
        }

        public float CameraMinZoomDistance
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsMinZoomDistance : fpsZoomDistance; }
        }

        public float CameraMaxZoomDistance
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsMaxZoomDistance : fpsZoomDistance; }
        }

        public Vector3 CameraTargetOffset
        {
            get
            {
                if (ViewMode == ShooterControllerViewMode.Tps)
                {
                    if (PlayerCharacterEntity.ExtraMovementState == ExtraMovementState.IsCrouching)
                    {
                        return tpsTargetOffsetWhileCrouching;
                    }
                    else if (PlayerCharacterEntity.ExtraMovementState == ExtraMovementState.IsCrawling)
                    {
                        return tpsTargetOffsetWhileCrawling;
                    }
                    else
                    {
                        return tpsTargetOffset;
                    }
                }
                return fpsTargetOffset;
            }
        }

        public float CameraFov
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsFov : fpsFov; }
        }

        public float CameraNearClipPlane
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsNearClipPlane : fpsNearClipPlane; }
        }

        public float CameraFarClipPlane
        {
            get { return ViewMode == ShooterControllerViewMode.Tps ? tpsFarClipPlane : fpsFarClipPlane; }
        }

        public float CurrentCameraFov
        {
            get { return CacheGameplayCamera.fieldOfView; }
            set { CacheGameplayCamera.fieldOfView = value; }
        }

        public float RotationSpeedScale
        {
            get { return CacheGameplayCameraControls.rotationSpeedScale; }
            set { CacheGameplayCameraControls.rotationSpeedScale = value; }
        }

        public bool HideCrosshair { get; set; }

        public float CurrentTurnSpeed
        {
            get
            {
                if (PlayerCharacterEntity.MovementState.HasFlag(MovementState.IsUnderWater))
                    return turnSpeedWileSwimming;
                switch (PlayerCharacterEntity.ExtraMovementState)
                {
                    case ExtraMovementState.IsSprinting:
                        return turnSpeedWhileSprinting;
                    case ExtraMovementState.IsCrouching:
                        return turnSpeedWhileCrouching;
                    case ExtraMovementState.IsCrawling:
                        return turnSpeedWileCrawling;
                }
                return turnSpeed;
            }
        }

        // Input data
        InputStateManager activateInput;
        InputStateManager pickupItemInput;
        InputStateManager reloadInput;
        InputStateManager exitVehicleInput;
        InputStateManager switchEquipWeaponSetInput;
        float lastPlayingAttackOrUseSkillAnimationTime;
        bool updatingInputs;
        // Entity detector
        NearbyEntityDetector warpPortalEntityDetector;
        // Temp physic variables
        RaycastHit[] raycasts = new RaycastHit[512];
        Collider[] overlapColliders = new Collider[512];
        RaycastHit tempHitInfo;
        // Temp target
        BasePlayerCharacterEntity targetPlayer;
        NpcEntity targetNpc;
        BuildingEntity targetBuilding;
        VehicleEntity targetVehicle;
        WarpPortalEntity targetWarpPortal;
        ItemsContainerEntity targetItemsContainer;
        // Temp data
        IGameEntity tempGameEntity;
        Ray centerRay;
        float centerOriginToCharacterDistance;
        Vector3 moveDirection;
        Vector3 cameraForward;
        Vector3 cameraRight;
        float inputV;
        float inputH;
        Vector2 normalizedInput;
        Vector3 moveLookDirection;
        Vector3 targetLookDirection;
        float tempDeltaTime;
        bool tempPressAttackRight;
        bool tempPressAttackLeft;
        bool tempPressWeaponAbility;
        bool isLeftHandAttacking;
        float pitch;
        Vector3 aimTargetPosition;
        Vector3 turnDirection;
        bool toggleSprintOn;
        bool toggleCrouchOn;
        bool toggleCrawlOn;
        // Controlling states
        ShooterControllerViewMode dirtyViewMode;
        IWeaponItem rightHandWeapon;
        IWeaponItem leftHandWeapon;
        MovementState movementState;
        ExtraMovementState extraMovementState;
        ShooterControllerViewMode? viewModeBeforeDead;
        bool updateAttackingCrosshair;
        bool updateAttackedCrosshair;
        bool mustReleaseFireKey;
        float buildYRotate;
        byte pauseFireInputFrames;
        //bool isDoingAction;
        //bool mustReleaseFireKey;
        //float buildYRotate;

        protected override void Awake()
        {
            base.Awake();
            if (gameplayCameraPrefab != null)
                CacheGameplayCameraControls = Instantiate(gameplayCameraPrefab);
            if (minimapCameraPrefab != null)
                CacheMinimapCameraControls = Instantiate(minimapCameraPrefab);
            buildingItemIndex = -1;
            isLeftHandAttacking = false;
            ConstructingBuildingEntity = null;
            activateInput = new InputStateManager("Activate");
            pickupItemInput = new InputStateManager("PickUpItem");
            reloadInput = new InputStateManager("Reload");
            exitVehicleInput = new InputStateManager("ExitVehicle");
            switchEquipWeaponSetInput = new InputStateManager("SwitchEquipWeaponSet");
            // Initialize warp portal entity detector
            GameObject tempGameObject = new GameObject("_WarpPortalEntityDetector");
            warpPortalEntityDetector = tempGameObject.AddComponent<NearbyEntityDetector>();
            warpPortalEntityDetector.detectingRadius = CurrentGameInstance.conversationDistance;
            warpPortalEntityDetector.findWarpPortal = true;
        }

        protected override void Setup(BasePlayerCharacterEntity characterEntity)
        {
            base.Setup(characterEntity);

            if (characterEntity == null)
                return;

            targetLookDirection = MovementTransform.forward;
            SetupEquipWeapons(characterEntity.EquipWeapons);
            characterEntity.onEquipWeaponSetChange += SetupEquipWeapons;
            characterEntity.onSelectableWeaponSetsOperation += SetupEquipWeapons;
            characterEntity.onLaunchDamageEntity += OnLaunchDamageEntity;
            characterEntity.ModelManager.InstantiateFpsModel(CacheGameplayCameraTransform);
            characterEntity.ModelManager.SetIsFps(ViewMode == ShooterControllerViewMode.Fps);
            CacheGameplayCameraControls.startYRotation = characterEntity.CurrentRotation.y;
            UpdateViewMode();
        }

        protected override void Desetup(BasePlayerCharacterEntity characterEntity)
        {
            base.Desetup(characterEntity);

            if (CacheGameplayCameraControls != null)
                CacheGameplayCameraControls.target = null;

            if (CacheMinimapCameraControls != null)
                CacheMinimapCameraControls.target = null;

            if (characterEntity == null)
                return;

            characterEntity.onEquipWeaponSetChange -= SetupEquipWeapons;
            characterEntity.onSelectableWeaponSetsOperation -= SetupEquipWeapons;
            characterEntity.onLaunchDamageEntity -= OnLaunchDamageEntity;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (CacheGameplayCameraControls != null)
                Destroy(CacheGameplayCameraControls.gameObject);
            if (CacheMinimapCameraControls != null)
                Destroy(CacheMinimapCameraControls.gameObject);
            if (warpPortalEntityDetector != null)
                Destroy(warpPortalEntityDetector.gameObject);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SetupEquipWeapons(byte equipWeaponSet)
        {
            SetupEquipWeapons(PlayerCharacterEntity.EquipWeapons);
        }

        private void SetupEquipWeapons(LiteNetLibManager.LiteNetLibSyncList.Operation operation, int index)
        {
            SetupEquipWeapons(PlayerCharacterEntity.EquipWeapons);
        }

        private void SetupEquipWeapons(EquipWeapons equipWeapons)
        {
            CurrentCrosshairSetting = PlayerCharacterEntity.GetCrosshairSetting();
            UpdateCrosshair(CurrentCrosshairSetting, false, -CurrentCrosshairSetting.shrinkPerFrame);

            rightHandWeapon = equipWeapons.GetRightHandWeaponItem();
            leftHandWeapon = equipWeapons.GetLeftHandWeaponItem();
            // Weapon ability will be able to use when equip weapon at main-hand only
            if (rightHandWeapon != null && leftHandWeapon == null)
            {
                if (rightHandWeapon.WeaponAbility != WeaponAbility)
                {
                    if (WeaponAbility != null)
                        WeaponAbility.Desetup();
                    WeaponAbility = rightHandWeapon.WeaponAbility;
                    if (WeaponAbility != null)
                        WeaponAbility.Setup(this, equipWeapons.rightHand);
                    WeaponAbilityState = WeaponAbilityState.Deactivated;
                }
            }
            else
            {
                if (WeaponAbility != null)
                    WeaponAbility.Desetup();
                WeaponAbility = null;
                WeaponAbilityState = WeaponAbilityState.Deactivated;
            }
			if (rightHandWeapon == null)
                rightHandWeapon = GameInstance.Singleton.DefaultWeaponItem;
            if (leftHandWeapon == null)
                leftHandWeapon = GameInstance.Singleton.DefaultWeaponItem;
        }

        protected override void Update()
        {
			if (pauseFireInputFrames > 0)
                --pauseFireInputFrames;
            if (PlayerCharacterEntity == null || !PlayerCharacterEntity.IsOwnerClient)
                return;

            if (CacheGameplayCameraControls != null)
                CacheGameplayCameraControls.target = CameraTargetTransform;

            if (CacheMinimapCameraControls != null)
                CacheMinimapCameraControls.target = CameraTargetTransform;

            if (PlayerCharacterEntity.IsDead())
            {
                // Deactivate weapon ability immediately when dead
                if (WeaponAbility != null && WeaponAbilityState != WeaponAbilityState.Deactivated)
                {
                    WeaponAbility.ForceDeactivated();
                    WeaponAbilityState = WeaponAbilityState.Deactivated;
                }
                // Set view mode to TPS when character dead
                if (!viewModeBeforeDead.HasValue)
                    viewModeBeforeDead = ViewMode;
                ViewMode = ShooterControllerViewMode.Tps;
            }
            else
            {
                // Set view mode to view mode before dead when character alive
                if (viewModeBeforeDead.HasValue)
                {
                    ViewMode = viewModeBeforeDead.Value;
                    viewModeBeforeDead = null;
                }
            }

            if (dirtyViewMode != viewMode)
                UpdateViewMode();

            CacheGameplayCameraControls.targetOffset = CameraTargetOffset;
            CacheGameplayCameraControls.enableWallHitSpring = viewMode == ShooterControllerViewMode.Tps ? true : false;
            CacheGameplayCameraControls.target = ViewMode == ShooterControllerViewMode.Fps ? PlayerCharacterEntity.FpsCameraTargetTransform : PlayerCharacterEntity.CameraTargetTransform;

            // Set temp data
            tempDeltaTime = Time.deltaTime;

            // Update inputs
            activateInput.OnUpdate(tempDeltaTime);
            pickupItemInput.OnUpdate(tempDeltaTime);
            reloadInput.OnUpdate(tempDeltaTime);
            exitVehicleInput.OnUpdate(tempDeltaTime);
            switchEquipWeaponSetInput.OnUpdate(tempDeltaTime);

            // Check is any UIs block controller or not?
            IsBlockController = CacheUISceneGameplay.IsBlockController();

            // Lock cursor when not show UIs
            if (InputManager.useMobileInputOnNonMobile || Application.isMobilePlatform)
            {
                // Control camera by touch-screen
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                CacheGameplayCameraControls.updateRotationX = false;
                CacheGameplayCameraControls.updateRotationY = false;
                CacheGameplayCameraControls.updateRotation = InputManager.GetButton("CameraRotate");
                CacheGameplayCameraControls.updateZoom = !IsBlockController;
            }
            else
            {
                // Control camera by mouse-move
                Cursor.lockState = !IsBlockController ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = IsBlockController;
                CacheGameplayCameraControls.updateRotation = !IsBlockController;
                CacheGameplayCameraControls.updateZoom = !IsBlockController;
            }
            // Clear selected entity
            SelectedEntity = null;

            // Update crosshair (with states from last update)
            UpdateCrosshair();

            // Clear controlling states from last update
            movementState = MovementState.None;
            extraMovementState = ExtraMovementState.None;
            UpdateActivatedWeaponAbility(tempDeltaTime);

            if (IsBlockController || GenericUtils.IsFocusInputField())
            {
                mustReleaseFireKey = false;

                PlayerCharacterEntity.KeyMovement(Vector3.zero, MovementState.None);
                DeactivateWeaponAbility();
                return;
            }

            // Prepare variables to find nearest raycasted hit point
            centerRay = CacheGameplayCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            centerOriginToCharacterDistance = Vector3.Distance(centerRay.origin, CacheTransform.position);
            cameraForward = CacheGameplayCameraTransform.forward;
            cameraRight = CacheGameplayCameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // Update look target and aim position
            if (ConstructingBuildingEntity == null)
                UpdateTarget_BattleMode();
            else
                UpdateTarget_BuildMode();

            // Update movement and camera pitch
            UpdateMovementInputs();

            // Update aim position
            PlayerCharacterEntity.AimPosition = PlayerCharacterEntity.GetAttackAimPosition(ref isLeftHandAttacking, aimTargetPosition);

            // Update input
            if (!updatingInputs)
            {
                if (ConstructingBuildingEntity == null)
                    UpdateInputs_BattleMode().Forget();
                else
                    UpdateInputs_BuildMode().Forget();
            }

            // Hide Npc UIs when move
            if (moveDirection.sqrMagnitude > 0f)
                HideNpcDialog();

            // If jumping add jump state
            if (InputManager.GetButtonDown("Jump"))
            {
                if (unToggleCrouchWhenJump && PlayerCharacterEntity.ExtraMovementState == ExtraMovementState.IsCrouching)
                    toggleCrouchOn = false;
                else if (unToggleCrawlWhenJump && PlayerCharacterEntity.ExtraMovementState == ExtraMovementState.IsCrawling)
                    toggleCrawlOn = false;
                else
                    movementState |= MovementState.IsJump;
            }
            else if (PlayerCharacterEntity.MovementState.HasFlag(MovementState.IsGrounded))
            {
                if (DetectExtraActive("Sprint", sprintActiveMode, ref toggleSprintOn))
                {
                    extraMovementState = ExtraMovementState.IsSprinting;
                    toggleCrouchOn = false;
                    toggleCrawlOn = false;
                }
                if (DetectExtraActive("Crouch", crouchActiveMode, ref toggleCrouchOn))
                {
                    extraMovementState = ExtraMovementState.IsCrouching;
                    toggleSprintOn = false;
                    toggleCrawlOn = false;
                }
                if (DetectExtraActive("Crawl", crawlActiveMode, ref toggleCrawlOn))
                {
                    extraMovementState = ExtraMovementState.IsCrawling;
                    toggleSprintOn = false;
                    toggleCrouchOn = false;
                }
            }

            PlayerCharacterEntity.KeyMovement(moveDirection, movementState);
            PlayerCharacterEntity.SetExtraMovement(extraMovementState);
            UpdateLookAtTarget();

            if (canSwitchViewMode && InputManager.GetButtonDown("SwitchViewMode"))
            {
                switch (ViewMode)
                {
                    case ShooterControllerViewMode.Tps:
                        ViewMode = ShooterControllerViewMode.Fps;
                        break;
                    case ShooterControllerViewMode.Fps:
                        ViewMode = ShooterControllerViewMode.Tps;
                        break;
                }
            }
        }

        private void LateUpdate()
        {
            if (PlayerCharacterEntity.MovementState.HasFlag(MovementState.IsUnderWater))
            {
                // Clear toggled sprint, crouch and crawl
                toggleSprintOn = false;
                toggleCrouchOn = false;
                toggleCrawlOn = false;
            }
            // Update inputs
            activateInput.OnLateUpdate();
            pickupItemInput.OnLateUpdate();
            reloadInput.OnLateUpdate();
            exitVehicleInput.OnLateUpdate();
            switchEquipWeaponSetInput.OnLateUpdate();
        }

        private bool DetectExtraActive(string key, ExtraMoveActiveMode activeMode, ref bool state)
        {
            switch (activeMode)
            {
                case ExtraMoveActiveMode.Hold:
                    state = InputManager.GetButton(key);
                    break;
                case ExtraMoveActiveMode.Toggle:
                    if (InputManager.GetButtonDown(key))
                        state = !state;
                    break;
            }
            return state;
        }

        private void UpdateTarget_BattleMode()
        {
            // Prepare raycast distance / fov
            float attackDistance = 0f;
            bool attacking = false;
            if (IsUsingHotkey())
            {
                mustReleaseFireKey = true;
            }
            else
            {
                // Attack with right hand weapon
                tempPressAttackRight = GetPrimaryAttackButton();
                if (WeaponAbility == null && leftHandWeapon != null)
                {
                    // Attack with left hand weapon if left hand weapon not empty
                    tempPressAttackLeft = GetSecondaryAttackButton();
                }
                else if (WeaponAbility != null)
                {
                    // Use weapon ability if it can
                    tempPressWeaponAbility = GetSecondaryAttackButtonDown();
                }

                attacking = tempPressAttackRight || tempPressAttackLeft;
                if (attacking && !PlayerCharacterEntity.IsAttacking && !PlayerCharacterEntity.IsUsingSkill)
                {
                    // Priority is right > left
                    isLeftHandAttacking = !tempPressAttackRight && tempPressAttackLeft;
                }

                // Calculate aim distance by skill or weapon
                if (PlayerCharacterEntity.UsingSkill != null && PlayerCharacterEntity.UsingSkill.IsAttack)
                {
                    // Increase aim distance by skill attack distance
                    attackDistance = PlayerCharacterEntity.UsingSkill.GetCastDistance(PlayerCharacterEntity, PlayerCharacterEntity.UsingSkillLevel, isLeftHandAttacking);
                    attacking = true;
                }
                else if (queueUsingSkill.skill != null && queueUsingSkill.skill.IsAttack)
                {
                    // Increase aim distance by skill attack distance
                    attackDistance = queueUsingSkill.skill.GetCastDistance(PlayerCharacterEntity, queueUsingSkill.level, isLeftHandAttacking);
                    attacking = true;
                }
                else
                {
                    // Increase aim distance by attack distance
                    attackDistance = PlayerCharacterEntity.GetAttackDistance(isLeftHandAttacking);
                }
            }
            // Temporary variables
            RaycastHit tempHitInfo;
            float tempDistance;
            // Default aim position (aim to sky/space)
            aimTargetPosition = centerRay.origin + centerRay.direction * (centerOriginToCharacterDistance + attackDistance);
            // Aim to damageable hit boxes (higher priority than other entities)
            // Raycast from camera position to center of screen
            int tempCount = PhysicUtils.SortedRaycastNonAlloc3D(centerRay.origin, centerRay.direction, raycasts, centerOriginToCharacterDistance + attackDistance, Physics.DefaultRaycastLayers);
            for (int tempCounter = 0; tempCounter < tempCount; ++tempCounter)
            {
                tempHitInfo = raycasts[tempCounter];

                if (tempHitInfo.transform.gameObject.layer == PhysicLayers.TransparentFX ||
                    tempHitInfo.transform.gameObject.layer == PhysicLayers.IgnoreRaycast ||
                    tempHitInfo.transform.gameObject.layer == PhysicLayers.Water)
                {
                    // Skip some layers
                    continue;
                }

                if (tempHitInfo.collider.GetComponent<IUnHittable>() != null)
                {
                    // Don't aim to unhittable objects
                    continue;
                }

                // Get damageable hit box component from hit target
                tempGameEntity = tempHitInfo.collider.GetComponent<DamageableHitBox>();

                if (tempGameEntity == null || !tempGameEntity.Entity || tempGameEntity.Entity.IsHide() ||
                    tempGameEntity.GetObjectId() == PlayerCharacterEntity.ObjectId)
                {
                    // Skip empty game entity / hiddeing entity / controlling player's entity
                    continue;
                }

                // Entity isn't in front of character, so it's not the target
                if (turnForwardWhileDoingAction && !IsInFront(tempHitInfo.point))
                    continue;

                // Skip dead entity while attacking (to allow to use resurrect skills)
                if (attacking && (tempGameEntity as DamageableHitBox).IsDead())
                    continue;

                // Entity is in front of character, so this is target
                aimTargetPosition = tempHitInfo.point;
                SelectedEntity = tempGameEntity.Entity;
                break;
            }

            // Aim to activateable entities if it can't find attacking target
            if (SelectedEntity == null)
            {
                // Default aim position (aim to sky/space)
                aimTargetPosition = centerRay.origin + centerRay.direction * (centerOriginToCharacterDistance + findTargetRaycastDistance);
                // Raycast from camera position to center of screen
                tempCount = PhysicUtils.SortedRaycastNonAlloc3D(centerRay.origin, centerRay.direction, raycasts, centerOriginToCharacterDistance + findTargetRaycastDistance, CurrentGameInstance.GetTargetLayerMask());
                for (int tempCounter = 0; tempCounter < tempCount; ++tempCounter)
                {
                    tempHitInfo = raycasts[tempCounter];
                    if (tempHitInfo.collider.GetComponent<IUnHittable>() != null)
                    {
                        // Don't aim to unhittable objects
                        continue;
                    }

                    // Get distance between character and raycast hit point
                    tempDistance = Vector3.Distance(CacheTransform.position, tempHitInfo.point);
                    tempGameEntity = tempHitInfo.collider.GetComponent<IGameEntity>();

                    if (tempGameEntity == null || !tempGameEntity.Entity || tempGameEntity.Entity.IsHide() ||
                        tempGameEntity.GetObjectId() == PlayerCharacterEntity.ObjectId)
                    {
                        // Skip empty game entity / hiddeing entity / controlling player's entity
                        continue;
                    }

                    // Find item drop entity
                    if (tempGameEntity.Entity is ItemDropEntity &&
                        tempDistance <= CurrentGameInstance.pickUpItemDistance)
                    {
                        // Entity is in front of character, so this is target
                        if (!turnForwardWhileDoingAction || IsInFront(tempHitInfo.point))
                            aimTargetPosition = tempHitInfo.point;
                        SelectedEntity = tempGameEntity.Entity;
                        break;
                    }
                    // Find activatable entity (NPC/Building/Mount/Etc)
                    if (tempDistance <= CurrentGameInstance.conversationDistance)
                    {
                        // Entity is in front of character, so this is target
                        if (!turnForwardWhileDoingAction || IsInFront(tempHitInfo.point))
                            aimTargetPosition = tempHitInfo.point;
                        SelectedEntity = tempGameEntity.Entity;
                        break;
                    }
                }
            }
            // Calculate aim direction
            turnDirection = aimTargetPosition - CacheTransform.position;
            turnDirection.y = 0f;
            turnDirection.Normalize();
            // Show target hp/mp
            CacheUISceneGameplay.SetTargetEntity(SelectedEntity);
            PlayerCharacterEntity.SetTargetEntity(SelectedEntity);
            // Update aim assist
            CacheGameplayCameraControls.enableAimAssist = enableAimAssist && (tempPressAttackRight || tempPressAttackLeft || !aimAssistOnFireOnly) && !(SelectedEntity is IDamageableEntity);
            CacheGameplayCameraControls.enableAimAssistX = enableAimAssistX;
            CacheGameplayCameraControls.enableAimAssistY = enableAimAssistY;
            CacheGameplayCameraControls.aimAssistRadius = aimAssistRadius;
            CacheGameplayCameraControls.aimAssistLayerMask = GetAimAssistLayerMask();
            CacheGameplayCameraControls.aimAssistXSpeed = aimAssistXSpeed;
            CacheGameplayCameraControls.aimAssistYSpeed = aimAssistYSpeed;
            CacheGameplayCameraControls.aimAssistMaxAngleFromFollowingTarget = 115f;
            CacheGameplayCameraControls.AimAssistAvoidanceListener = this;
        }

        public bool AvoidAimAssist(RaycastHit hitInfo)
        {
            IGameEntity entity = hitInfo.collider.GetComponent<IGameEntity>();
            if (entity != null && entity.Entity != null && entity.Entity != PlayerCharacterEntity)
            {
                DamageableEntity damageableEntity = entity.Entity as DamageableEntity;
                return damageableEntity == null || damageableEntity.IsDead();
            }
            return true;
        }

        private int GetAimAssistLayerMask()
        {
            int layerMask = 0;
            if (aimAssistCharacter)
                layerMask = layerMask | CurrentGameInstance.characterLayer.Mask;
            if (aimAssistBuilding)
                layerMask = layerMask | CurrentGameInstance.buildingLayer.Mask;
            if (aimAssistHarvestable)
                layerMask = layerMask | CurrentGameInstance.harvestableLayer.Mask;
            return layerMask;
        }

        private void UpdateTarget_BuildMode()
        {
            // Disable aim assist while constucting the building
            CacheGameplayCameraControls.enableAimAssist = false;
        }

        private void UpdateMovementInputs()
        {
            pitch = CacheGameplayCameraTransform.eulerAngles.x;

            // Update charcter pitch
            PlayerCharacterEntity.Pitch = pitch;

            // If mobile platforms, don't receive input raw to make it smooth
            bool raw = !InputManager.useMobileInputOnNonMobile && !Application.isMobilePlatform;
            moveDirection = Vector3.zero;
            inputV = InputManager.GetAxis("Vertical", raw);
            inputH = InputManager.GetAxis("Horizontal", raw);
            normalizedInput = new Vector2(inputV, inputH).normalized;
            moveDirection += cameraForward * inputV;
            moveDirection += cameraRight * inputH;
            if (moveDirection.sqrMagnitude > 0f)
            {
                if (pitch > 180f)
                    pitch -= 360f;
                moveDirection.y = -pitch / 90f;
            }
            // Set movement state by inputs
            switch (Mode)
            {
                case ControllerMode.Adventure:
                    if (normalizedInput.x > 0.5f || normalizedInput.x < -0.5f || normalizedInput.y > 0.5f || normalizedInput.y < -0.5f)
                        movementState = MovementState.Forward;
                    moveLookDirection = moveDirection;
                    moveLookDirection.y = 0f;
                    break;
                case ControllerMode.Combat:
                    if (normalizedInput.x > 0.5f)
                        movementState |= MovementState.Forward;
                    else if (normalizedInput.x < -0.5f)
                        movementState |= MovementState.Backward;
                    if (normalizedInput.y > 0.5f)
                        movementState |= MovementState.Right;
                    else if (normalizedInput.y < -0.5f)
                        movementState |= MovementState.Left;
                    moveLookDirection = cameraForward;
                    break;
            }

            if (ViewMode == ShooterControllerViewMode.Fps)
            {
                // Force turn to look direction
                moveLookDirection = cameraForward;
                targetLookDirection = cameraForward;
            }

            moveDirection.Normalize();
        }

        private async UniTaskVoid UpdateInputs_BattleMode()
        {
            updatingInputs = true;
            FireType rightHandFireType = FireType.SingleFire;
            FireType leftHandFireType = FireType.SingleFire;
            if (rightHandWeapon != null)
                rightHandFireType = rightHandWeapon.FireType;
            if (leftHandWeapon != null)
                leftHandFireType = leftHandWeapon.FireType;
            // Have to release fire key, then check press fire key later on next frame
            if (mustReleaseFireKey)
            {
                tempPressAttackRight = false;
                tempPressAttackLeft = false;
                if (!isLeftHandAttacking &&
                    (GetPrimaryAttackButtonUp() ||
                    !GetPrimaryAttackButton()))
                {
                    mustReleaseFireKey = false;
                    // Button released, start attacking while fire type is fire on release
                    if (rightHandFireType == FireType.FireOnRelease)
                        Attack(isLeftHandAttacking);
                }
                if (isLeftHandAttacking &&
                    (GetSecondaryAttackButtonUp() ||
                    !GetSecondaryAttackButton()))
                {
                    mustReleaseFireKey = false;
                    // Button released, start attacking while fire type is fire on release
                    if (leftHandFireType == FireType.FireOnRelease)
                        Attack(isLeftHandAttacking);
                }
            }
            if (PlayerCharacterEntity.IsPlayingAttackOrUseSkillAnimation())
                lastPlayingAttackOrUseSkillAnimationTime = Time.unscaledTime;
            bool anyKeyPressed = false;
            bool activatingEntityOrDoAction = false;
            if (queueUsingSkill.skill != null ||
                tempPressAttackRight ||
                tempPressAttackLeft ||
                activateInput.IsPress ||
                activateInput.IsRelease ||
                activateInput.IsHold ||
                PlayerCharacterEntity.IsPlayingActionAnimation())
            {
                anyKeyPressed = true;
                // Find forward character / npc / building / warp entity from camera center
                // Check is character playing action animation to turn character forwarding to aim position
                targetPlayer = null;
                targetNpc = null;
                targetBuilding = null;
                targetVehicle = null;
                targetWarpPortal = null;
                targetItemsContainer = null;
                if (!tempPressAttackRight && !tempPressAttackLeft)
                {
                    if (activateInput.IsHold)
                    {
                        if (SelectedEntity is BuildingEntity)
                        {
                            activatingEntityOrDoAction = true;
                            targetBuilding = SelectedEntity as BuildingEntity;
                        }
                    }
                    else if (activateInput.IsRelease)
                    {
                        if (SelectedEntity == null)
                        {
                            if (warpPortalEntityDetector?.warpPortals.Count > 0)
                            {
                                activatingEntityOrDoAction = true;
                                // It may not able to raycast from inside warp portal, so try to get it from the detector
                                targetWarpPortal = warpPortalEntityDetector.warpPortals[0];
                            }
                        }
                        else
                        {
                            if (SelectedEntity is BasePlayerCharacterEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetPlayer = SelectedEntity as BasePlayerCharacterEntity;
                            }
                            if (SelectedEntity is NpcEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetNpc = SelectedEntity as NpcEntity;
                            }
                            if (SelectedEntity is BuildingEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetBuilding = SelectedEntity as BuildingEntity;
                            }
                            if (SelectedEntity is VehicleEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetVehicle = SelectedEntity as VehicleEntity;
                            }
                            if (SelectedEntity is WarpPortalEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetWarpPortal = SelectedEntity as WarpPortalEntity;
                            }
                            if (SelectedEntity is ItemsContainerEntity)
                            {
                                activatingEntityOrDoAction = true;
                                targetItemsContainer = SelectedEntity as ItemsContainerEntity;
                            }
                        }
                    }
                }

                // Update look direction
                if (PlayerCharacterEntity.IsPlayingAttackOrUseSkillAnimation())
                {
                    activatingEntityOrDoAction = true;
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                }
                else if (queueUsingSkill.skill != null)
                {
                    activatingEntityOrDoAction = true;
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                    UpdateLookAtTarget();
                    UseSkill(isLeftHandAttacking);
                }
                else if (tempPressAttackRight || tempPressAttackLeft)
                {
                    activatingEntityOrDoAction = true;
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                    UpdateLookAtTarget();
                    if (!isLeftHandAttacking)
                    {
                        // Fire on release weapons have to release to fire, so when start holding, play weapon charge animation
                        if (rightHandFireType == FireType.FireOnRelease)
                            WeaponCharge(isLeftHandAttacking);
                        else
                            Attack(isLeftHandAttacking);
                    }
                    else
                    {
                        // Fire on release weapons have to release to fire, so when start holding, play weapon charge animation
                        if (leftHandFireType == FireType.FireOnRelease)
                            WeaponCharge(isLeftHandAttacking);
                        else
                            Attack(isLeftHandAttacking);
                    }
                }
                else if (activateInput.IsHold && activatingEntityOrDoAction)
                {
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                    UpdateLookAtTarget();
                    HoldActivate();
                }
                else if (activateInput.IsRelease && activatingEntityOrDoAction)
                {
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                    UpdateLookAtTarget();
                    Activate();
                }
                else
                {
                    SetTargetLookDirectionWhileMoving();
                }
            }

            if (tempPressWeaponAbility && !activatingEntityOrDoAction)
            {
                anyKeyPressed = true;
                // Toggle weapon ability
                switch (WeaponAbilityState)
                {
                    case WeaponAbilityState.Activated:
                    case WeaponAbilityState.Activating:
                        DeactivateWeaponAbility();
                        break;
                    case WeaponAbilityState.Deactivated:
                    case WeaponAbilityState.Deactivating:
                        ActivateWeaponAbility();
                        break;
                }
            }

            if (pickupItemInput.IsPress && !activatingEntityOrDoAction)
            {
                anyKeyPressed = true;

                // If target is entity with lootbag, open it
                if (SelectedEntity != null && SelectedEntity is BaseCharacterEntity)
                {
                    BaseCharacterEntity c = SelectedEntity as BaseCharacterEntity;
                    if (c != null && c.IsDead() && c.useLootBag)
                        (CacheUISceneGameplay as UISceneGameplay).OnShowLootBag(c);
                }
                // Otherwise find item to pick up
                else if (SelectedEntity != null && SelectedEntity is ItemDropEntity)
                {
                    activatingEntityOrDoAction = true;
                    PlayerCharacterEntity.CallServerPickupItem(SelectedEntity.ObjectId);
                }
            }

            if (reloadInput.IsPress && !activatingEntityOrDoAction)
            {
                anyKeyPressed = true;
                // Reload ammo when press the button
                Reload();
            }

            if (exitVehicleInput.IsPress && !activatingEntityOrDoAction)
            {
                anyKeyPressed = true;
                // Exit vehicle
                PlayerCharacterEntity.CallServerExitVehicle();
            }

            if (switchEquipWeaponSetInput.IsPress && !activatingEntityOrDoAction)
            {
                anyKeyPressed = true;
                // Switch equip weapon set
                GameInstance.ClientInventoryHandlers.RequestSwitchEquipWeaponSet(new RequestSwitchEquipWeaponSetMessage()
                {
                    equipWeaponSet = (byte)(PlayerCharacterEntity.EquipWeaponSet + 1),
                }, ClientInventoryActions.ResponseSwitchEquipWeaponSet);
            }

            // Setup releasing state
            if (tempPressAttackRight && rightHandFireType != FireType.Automatic)
            {
                // The weapon's fire mode is single fire or fire on release, so player have to release fire key for next fire
                mustReleaseFireKey = true;
            }
            else if (tempPressAttackLeft && leftHandFireType != FireType.Automatic)
            {
                // The weapon's fire mode is single fire or fire on release, so player have to release fire key for next fire
                mustReleaseFireKey = true;
            }

            // Reloading
            if (PlayerCharacterEntity.EquipWeapons.rightHand.IsAmmoEmpty() ||
                PlayerCharacterEntity.EquipWeapons.leftHand.IsAmmoEmpty())
            {
                switch (emptyAmmoAutoReload)
                {
                    case EmptyAmmoAutoReload.ReloadImmediately:
                        Reload();
                        break;
                    case EmptyAmmoAutoReload.ReloadOnKeysReleased:
                        // Auto reload when ammo empty
                        if (!tempPressAttackRight && !tempPressAttackLeft && !reloadInput.IsPress)
                        {
                            // Reload ammo when empty and not press any keys
                            Reload();
                        }
                        break;
                }
            }

            // Update look direction
            if (!anyKeyPressed && !activatingEntityOrDoAction)
            {
                // Update look direction while moving without doing any action
                if (Time.unscaledTime - lastPlayingAttackOrUseSkillAnimationTime < stoppedPlayingAttackOrUseSkillAnimationDelay)
                {
                    activatingEntityOrDoAction = true;
                    while (!SetTargetLookDirectionWhileDoingAction())
                    {
                        await UniTask.Yield();
                    }
                }
                else
                {
                    SetTargetLookDirectionWhileMoving();
                }
            }

            updatingInputs = false;
        }

        private async UniTaskVoid UpdateInputs_BuildMode()
        {
            SetTargetLookDirectionWhileMoving();
            updatingInputs = false;
            await UniTask.Yield();
        }

        private void UpdateCrosshair()
        {
            bool isMoving = movementState.HasFlag(MovementState.Forward) ||
                movementState.HasFlag(MovementState.Backward) ||
                movementState.HasFlag(MovementState.Left) ||
                movementState.HasFlag(MovementState.Right) ||
                movementState.HasFlag(MovementState.IsJump);
            if (updateAttackingCrosshair)
            {
                UpdateCrosshair(CurrentCrosshairSetting, true, CurrentCrosshairSetting.expandPerFrameWhileAttacking);
                updateAttackingCrosshair = false;
                updateAttackedCrosshair = true;
            }
            else if (updateAttackedCrosshair)
            {
                UpdateCrosshair(CurrentCrosshairSetting, true, CurrentCrosshairSetting.shrinkPerFrameWhenAttacked);
                updateAttackedCrosshair = false;
            }
            else if (isMoving)
            {
                UpdateCrosshair(CurrentCrosshairSetting, false, CurrentCrosshairSetting.expandPerFrameWhileMoving);
            }
            else
            {
                UpdateCrosshair(CurrentCrosshairSetting, false, -CurrentCrosshairSetting.shrinkPerFrame);
            }
        }

        protected virtual void UpdateCrosshair(CrosshairSetting setting, bool isAttack, float power)
        {
            if (crosshairRect == null)
                return;
            // Show cross hair if weapon's crosshair setting isn't hidden or there is a constructing building
            crosshairRect.gameObject.SetActive((!setting.hidden && !HideCrosshair) || ConstructingBuildingEntity != null);
            // Not active?, don't update
            if (!crosshairRect.gameObject)
                return;
            // Change crosshair size by power
            Vector3 sizeDelta = CurrentCrosshairSize;
            // Expanding
            sizeDelta.x += power;
            sizeDelta.y += power;
            if (!isAttack)
                sizeDelta = new Vector2(Mathf.Clamp(sizeDelta.x, setting.minSpread, setting.maxSpread), Mathf.Clamp(sizeDelta.y, setting.minSpread, setting.maxSpread));
            crosshairRect.sizeDelta = CurrentCrosshairSize = sizeDelta;
        }

        private void UpdateRecoil()
        {
            float recoilX;
            float recoilY;
            if (movementState.HasFlag(MovementState.Forward) ||
                movementState.HasFlag(MovementState.Backward) ||
                movementState.HasFlag(MovementState.Left) ||
                movementState.HasFlag(MovementState.Right))
            {
                if (movementState.HasFlag(MovementState.IsUnderWater))
                {
                    recoilX = CurrentCrosshairSetting.recoilX * recoilRateWhileSwimming;
                    recoilY = CurrentCrosshairSetting.recoilY * recoilRateWhileSwimming;
                }
                else if (extraMovementState == ExtraMovementState.IsSprinting)
                {
                    recoilX = CurrentCrosshairSetting.recoilX * recoilRateWhileSprinting;
                    recoilY = CurrentCrosshairSetting.recoilY * recoilRateWhileSprinting;
                }
                else
                {
                    recoilX = CurrentCrosshairSetting.recoilX * recoilRateWhileMoving;
                    recoilY = CurrentCrosshairSetting.recoilY * recoilRateWhileMoving;
                }
            }
            else if (extraMovementState == ExtraMovementState.IsCrouching)
            {
                recoilX = CurrentCrosshairSetting.recoilX * recoilRateWhileCrouching;
                recoilY = CurrentCrosshairSetting.recoilY * recoilRateWhileCrouching;
            }
            else if (extraMovementState == ExtraMovementState.IsCrawling)
            {
                recoilX = CurrentCrosshairSetting.recoilX * recoilRateWhileCrawling;
                recoilY = CurrentCrosshairSetting.recoilY * recoilRateWhileCrawling;
            }
            else
            {
                recoilX = CurrentCrosshairSetting.recoilX;
                recoilY = CurrentCrosshairSetting.recoilY;
            }
            if (recoilX > 0f || recoilY > 0f)
            {
                CacheGameplayCameraControls.Recoil(recoilY, Random.Range(-recoilX, recoilX));
            }
        }

        private void OnLaunchDamageEntity(bool isLeftHand, CharacterItem weapon, Dictionary<DamageElement, MinMaxFloat> damageAmounts, BaseSkill skill, short skillLevel, int randomSeed, AimPosition aimPosition, Vector3 stagger, HashSet<DamageHitObjectInfo> hitObjectIds)
        {
            UpdateRecoil();
        }

        /// <summary>
        /// Return true if it's turned forwarding
        /// </summary>
        /// <returns></returns>
        private bool SetTargetLookDirectionWhileDoingAction()
        {
            switch (ViewMode)
            {
                case ShooterControllerViewMode.Fps:
                    // Just look at camera forward while character playing action animation
                    targetLookDirection = cameraForward;
                    return true;
                case ShooterControllerViewMode.Tps:
                    // Just look at camera forward while character playing action animation while `turnForwardWhileDoingAction` is `true`
                    Vector3 doActionLookDirection = turnForwardWhileDoingAction ? cameraForward : turnDirection;
                    if (turnSpeedWileDoingAction > 0f)
                    {
                        Quaternion currentRot = Quaternion.LookRotation(targetLookDirection);
                        Quaternion targetRot = Quaternion.LookRotation(doActionLookDirection);
                        currentRot = Quaternion.Slerp(currentRot, targetRot, turnSpeedWileDoingAction * Time.deltaTime);
                        targetLookDirection = currentRot * Vector3.forward;
                        return Quaternion.Angle(currentRot, targetRot) <= 15f;
                    }
                    else
                    {
                        // Turn immediately because turn speed <= 0
                        targetLookDirection = doActionLookDirection;
                        return true;
                    }
            }
            return false;
        }

        private void SetTargetLookDirectionWhileMoving()
        {
            switch (ViewMode)
            {
                case ShooterControllerViewMode.Fps:
                    // Just look at camera forward while character playing action animation
                    targetLookDirection = cameraForward;
                    break;
                case ShooterControllerViewMode.Tps:
                    // Turn character look direction to move direction while moving without doing any action
                    if (moveDirection.sqrMagnitude > 0f)
                    {
                        float currentTurnSpeed = CurrentTurnSpeed;
                        if (currentTurnSpeed > 0f)
                        {
                            Quaternion currentRot = Quaternion.LookRotation(targetLookDirection);
                            Quaternion targetRot = Quaternion.LookRotation(moveLookDirection);
                            currentRot = Quaternion.Slerp(currentRot, targetRot, currentTurnSpeed * Time.deltaTime);
                            targetLookDirection = currentRot * Vector3.forward;
                        }
                        else
                        {
                            // Turn immediately because turn speed <= 0
                            targetLookDirection = moveLookDirection;
                        }
                    }
                    break;
            }
        }

        private void UpdateLookAtTarget()
        {
            // Turn character to look direction immediately
            PlayerCharacterEntity.SetLookRotation(Quaternion.LookRotation(targetLookDirection));
        }

        public override void UseHotkey(HotkeyType type, string relateId, AimPosition aimPosition)
        {
            ClearQueueUsingSkill();
            switch (type)
            {
                case HotkeyType.Skill:
                    UseSkill(relateId, aimPosition);
                    break;
                case HotkeyType.Item:
                    UseItem(relateId, aimPosition);
                    break;
            }
        }

        private void UseSkill(string id, AimPosition aimPosition)
        {
            BaseSkill skill;
            short skillLevel;
            if (!GameInstance.Skills.TryGetValue(BaseGameData.MakeDataId(id), out skill) || skill == null ||
                !PlayerCharacterEntity.GetCaches().Skills.TryGetValue(skill, out skillLevel))
                return;
            SetQueueUsingSkill(aimPosition, skill, skillLevel);
        }

        private void UseItem(string id, AimPosition aimPosition)
        {
            int itemIndex;
            BaseItem item;
            int dataId = BaseGameData.MakeDataId(id);
            if (GameInstance.Items.ContainsKey(dataId))
            {
                item = GameInstance.Items[dataId];
                itemIndex = OwningCharacter.IndexOfNonEquipItem(dataId);
            }
            else
            {
                InventoryType inventoryType;
                byte equipWeaponSet;
                CharacterItem characterItem;
                if (PlayerCharacterEntity.IsEquipped(
                    id,
                    out inventoryType,
                    out itemIndex,
                    out equipWeaponSet,
                    out characterItem))
                {
                    GameInstance.ClientInventoryHandlers.RequestUnEquipItem(
                        inventoryType,
                        (short)itemIndex,
                        equipWeaponSet,
                        -1,
                        ClientInventoryActions.ResponseUnEquipArmor,
                        ClientInventoryActions.ResponseUnEquipWeapon);
                    return;
                }
                item = characterItem.GetItem();
            }

            if (itemIndex < 0)
                return;

            if (item == null)
                return;

            if (item.IsEquipment())
            {
                GameInstance.ClientInventoryHandlers.RequestEquipItem(
                        PlayerCharacterEntity,
                        (short)itemIndex,
                        ClientInventoryActions.ResponseEquipArmor,
                        ClientInventoryActions.ResponseEquipWeapon);
            }
            else if (item.IsSkill())
            {
                SetQueueUsingSkill(aimPosition, (item as ISkillItem).UsingSkill, (item as ISkillItem).UsingSkillLevel, (short)itemIndex);
            }
            else if (item.IsBuilding())
            {
                buildingItemIndex = itemIndex;
                if (showConfirmConstructionUI)
                {
                    // Show confirm UI
                    ShowConstructBuildingDialog();
                }
                else
                {
                    // Build when click
                    ConfirmBuild();
                }
                mustReleaseFireKey = true;
            }
            else if (item.IsUsable())
            {
                PlayerCharacterEntity.CallServerUseItem((short)itemIndex);
            }
        }

        public void Attack(bool isLeftHand)
        {
            if (pauseFireInputFrames > 0)
                return;
            // Set this to `TRUE` to update crosshair
            if (PlayerCharacterEntity.Attack(isLeftHand))
                updateAttackingCrosshair = true;
        }

        public void WeaponCharge(bool isLeftHand)
        {
            if (pauseFireInputFrames > 0)
                return;
            PlayerCharacterEntity.StartCharge(isLeftHand);
        }

        public void Reload()
        {
            if (WeaponAbility != null && WeaponAbility.ShouldDeactivateWhenReload)
                WeaponAbility.ForceDeactivated();
            // Reload ammo at server
            if (!PlayerCharacterEntity.EquipWeapons.rightHand.IsAmmoFull())
                PlayerCharacterEntity.Reload(false);
            else if (!PlayerCharacterEntity.EquipWeapons.leftHand.IsAmmoFull())
                PlayerCharacterEntity.Reload(true);
        }

        public void ActivateWeaponAbility()
        {
            if (WeaponAbility == null)
                return;

            if (WeaponAbilityState == WeaponAbilityState.Activated ||
                WeaponAbilityState == WeaponAbilityState.Activating)
                return;

            WeaponAbility.OnPreActivate();
            WeaponAbilityState = WeaponAbilityState.Activating;
        }

        private void UpdateActivatedWeaponAbility(float deltaTime)
        {
            if (WeaponAbility == null)
                return;

            if (WeaponAbilityState == WeaponAbilityState.Activated ||
                WeaponAbilityState == WeaponAbilityState.Deactivated)
                return;

            WeaponAbilityState = WeaponAbility.UpdateActivation(WeaponAbilityState, deltaTime);
        }

        private void DeactivateWeaponAbility()
        {
            if (WeaponAbility == null)
                return;

            if (WeaponAbilityState == WeaponAbilityState.Deactivated ||
                WeaponAbilityState == WeaponAbilityState.Deactivating)
                return;

            WeaponAbility.OnPreDeactivate();
            WeaponAbilityState = WeaponAbilityState.Deactivating;
        }

        public void HoldActivate()
        {
            if (targetBuilding != null)
            {
                TargetEntity = targetBuilding;
                ShowCurrentBuildingDialog();
            }
        }

        public void Activate()
        {
            // Priority Player -> Npc -> Buildings
            if (targetPlayer != null)
                CacheUISceneGameplay.SetActivePlayerCharacter(targetPlayer);
            else if (targetNpc != null)
                PlayerCharacterEntity.CallServerNpcActivate(targetNpc.ObjectId);
            else if (targetBuilding != null)
                ActivateBuilding(targetBuilding);
            else if (targetVehicle != null)
                PlayerCharacterEntity.CallServerEnterVehicle(targetVehicle.ObjectId);
            else if (targetWarpPortal != null)
                PlayerCharacterEntity.CallServerEnterWarp(targetWarpPortal.ObjectId);
            else if (targetItemsContainer != null)
                ShowItemsContainerDialog(targetItemsContainer);
        }

        public void UseSkill(bool isLeftHand)
        {
            if (pauseFireInputFrames > 0)
                return;
            if (queueUsingSkill.skill != null)
            {
                if (queueUsingSkill.itemIndex >= 0)
                {
                    PlayerCharacterEntity.UseSkillItem(queueUsingSkill.itemIndex, isLeftHand, SelectedEntityObjectId, queueUsingSkill.aimPosition);
                }
                else
                {
                    PlayerCharacterEntity.UseSkill(queueUsingSkill.skill.DataId, isLeftHand, SelectedEntityObjectId, queueUsingSkill.aimPosition);
                }
            }
            ClearQueueUsingSkill();
        }

        public int OverlapObjects(Vector3 position, float distance, int layerMask)
        {
            return Physics.OverlapSphereNonAlloc(position, distance, overlapColliders, layerMask);
        }

        public bool FindTarget(GameObject target, float actDistance, int layerMask)
        {
            int tempCount = OverlapObjects(CacheTransform.position, actDistance, layerMask);
            for (int tempCounter = 0; tempCounter < tempCount; ++tempCounter)
            {
                if (overlapColliders[tempCounter].gameObject == target)
                    return true;
            }
            return false;
        }

        public bool IsUsingHotkey()
        {
            // Check using hotkey for PC only
            if (!InputManager.useMobileInputOnNonMobile &&
                !Application.isMobilePlatform &&
                UICharacterHotkeys.UsingHotkey != null)
            {
                return true;
            }
            return false;
        }

        public bool GetPrimaryAttackButton()
        {
            return InputManager.GetButton("Fire1") || InputManager.GetButton("Attack");
        }

        public bool GetSecondaryAttackButton()
        {
            return InputManager.GetButton("Fire2");
        }

        public bool GetPrimaryAttackButtonUp()
        {
            return InputManager.GetButtonUp("Fire1") || InputManager.GetButtonUp("Attack");
        }

        public bool GetSecondaryAttackButtonUp()
        {
            return InputManager.GetButtonUp("Fire2");
        }

        public bool GetPrimaryAttackButtonDown()
        {
            return InputManager.GetButtonDown("Fire1") || InputManager.GetButtonDown("Attack");
        }

        public bool GetSecondaryAttackButtonDown()
        {
            return InputManager.GetButtonDown("Fire2");
        }

        public void UpdateViewMode()
        {
            dirtyViewMode = viewMode;
            UpdateCameraSettings();
            // Update camera zoom distance when change view mode only, to allow zoom controls
            CacheGameplayCameraControls.zoomDistance = CameraZoomDistance;
            CacheGameplayCameraControls.minZoomDistance = CameraMinZoomDistance;
            CacheGameplayCameraControls.maxZoomDistance = CameraMaxZoomDistance;
        }

        public virtual void UpdateCameraSettings()
        {
            CacheGameplayCamera.fieldOfView = CameraFov;
            CacheGameplayCamera.nearClipPlane = CameraNearClipPlane;
            CacheGameplayCamera.farClipPlane = CameraFarClipPlane;
            PlayerCharacterEntity.ModelManager.SetIsFps(viewMode == ShooterControllerViewMode.Fps);
        }

        public bool IsInFront(Vector3 position)
        {
            return Vector3.Angle(cameraForward, position - CacheTransform.position) < 115f;
        }

        public override AimPosition UpdateBuildAimControls(Vector2 aimAxes, BuildingEntity prefab)
        {
            // Instantiate constructing building
            if (ConstructingBuildingEntity == null)
            {
                InstantiateConstructingBuilding(prefab);
                buildYRotate = 0f;
            }
            // Rotate by keys
            Vector3 buildingAngles = Vector3.zero;
            if (CurrentGameInstance.DimensionType == DimensionType.Dimension3D)
            {
                if (buildRotationSnap)
                {
                    if (InputManager.GetButtonDown("RotateLeft"))
                        buildYRotate -= buildRotateAngle;
                    if (InputManager.GetButtonDown("RotateRight"))
                        buildYRotate += buildRotateAngle;
                    // Make Y rotation set to 0, 90, 180
                    buildingAngles.y = buildYRotate = Mathf.Round(buildYRotate / buildRotateAngle) * buildRotateAngle;
                }
                else
                {
                    float deltaTime = Time.deltaTime;
                    if (InputManager.GetButton("RotateLeft"))
                        buildYRotate -= buildRotateSpeed * deltaTime;
                    if (InputManager.GetButton("RotateRight"))
                        buildYRotate += buildRotateSpeed * deltaTime;
                    // Rotate by set angles
                    buildingAngles.y = buildYRotate;
                }
                ConstructingBuildingEntity.BuildYRotation = buildYRotate;
            }
            // Clear area before next find
            ConstructingBuildingEntity.BuildingArea = null;
            // Default aim position (aim to sky/space)
            aimTargetPosition = centerRay.origin + centerRay.direction * (centerOriginToCharacterDistance + findTargetRaycastDistance);
            // Raycast from camera position to center of screen
            FindConstructingBuildingArea(centerRay, centerOriginToCharacterDistance + findTargetRaycastDistance);
            // Not hit ground, find ground to snap
            if (!ConstructingBuildingEntity.HitSurface || !ConstructingBuildingEntity.IsPositionInBuildDistance(CacheTransform.position, aimTargetPosition))
            {
                aimTargetPosition = GameplayUtils.ClampPosition(CacheTransform.position, aimTargetPosition, ConstructingBuildingEntity.BuildDistance - BuildingEntity.BUILD_DISTANCE_BUFFER);
                // Find nearest grounded position
                FindConstructingBuildingArea(new Ray(aimTargetPosition, Vector3.down), 100f);
            }
            // Place constructing building
            if ((ConstructingBuildingEntity.BuildingArea && !ConstructingBuildingEntity.BuildingArea.snapBuildingObject) ||
                !ConstructingBuildingEntity.BuildingArea)
            {
                // Place the building on the ground when the building area is not snapping
                // Or place it anywhere if there is no building area
                // It's also no snapping build area, so set building rotation by camera look direction
                ConstructingBuildingEntity.Position = aimTargetPosition;
                // Rotate to camera
                Vector3 direction = aimTargetPosition - CacheGameplayCameraTransform.position;
                direction.y = 0f;
                direction.Normalize();
                ConstructingBuildingEntity.CacheTransform.eulerAngles = Quaternion.LookRotation(direction).eulerAngles + (Vector3.up * buildYRotate);
            }
            return AimPosition.CreatePosition(ConstructingBuildingEntity.Position);
        }

        private int FindConstructingBuildingArea(Ray ray, float distance)
        {
            ConstructingBuildingEntity.BuildingArea = null;
            ConstructingBuildingEntity.HitSurface = false;
            int tempCount = PhysicUtils.SortedRaycastNonAlloc3D(ray.origin, ray.direction, raycasts, distance, CurrentGameInstance.GetBuildLayerMask());
            RaycastHit tempHitInfo;
            BuildingEntity buildingEntity;
            BuildingArea buildingArea;
            for (int tempCounter = 0; tempCounter < tempCount; ++tempCounter)
            {
                tempHitInfo = raycasts[tempCounter];
                if (ConstructingBuildingEntity.CacheTransform.root == tempHitInfo.transform.root)
                {
                    // Hit collider which is part of constructing building entity, skip it
                    continue;
                }

                aimTargetPosition = tempHitInfo.point;

                if (!IsInFront(tempHitInfo.point))
                {
                    // Skip because this position is not allowed to build the building
                    continue;
                }

                // Find ground position from upper position
                Vector3 raycastOrigin = new Vector3(tempHitInfo.point.x, tempHitInfo.collider.bounds.center.y + tempHitInfo.collider.bounds.extents.y + 0.01f, tempHitInfo.point.z);
                RaycastHit[] groundHits = Physics.RaycastAll(raycastOrigin, Vector3.down, tempHitInfo.collider.bounds.size.y + 0.01f, CurrentGameInstance.GetBuildLayerMask());
                for (int j = 0; j < groundHits.Length; ++j)
                {
                    if (groundHits[j].transform == tempHitInfo.transform)
                        aimTargetPosition = groundHits[j].point;
                }

                buildingEntity = tempHitInfo.transform.root.GetComponent<BuildingEntity>();
                buildingArea = tempHitInfo.transform.GetComponent<BuildingArea>();
                if ((buildingArea == null || !ConstructingBuildingEntity.BuildingTypes.Contains(buildingArea.buildingType))
                    && buildingEntity == null)
                {
                    // Hit surface which is not building area or building entity
                    ConstructingBuildingEntity.BuildingArea = null;
                    ConstructingBuildingEntity.HitSurface = true;
                    break;
                }

                if (buildingArea == null || !ConstructingBuildingEntity.BuildingTypes.Contains(buildingArea.buildingType))
                {
                    // Skip because this area is not allowed to build the building that you are going to build
                    continue;
                }

                // Found building area which can construct the building
                ConstructingBuildingEntity.BuildingArea = buildingArea;
                ConstructingBuildingEntity.HitSurface = true;
                break;
            }
            return tempCount;
        }

        public override void FinishBuildAimControls(bool isCancel)
        {
            if (isCancel)
                CancelBuild();
        }

        public override void ConfirmBuild()
        {
            base.ConfirmBuild();
            pauseFireInputFrames = PAUSE_FIRE_INPUT_FRAMES_AFTER_CONFIRM_BUILD;
        }
    }
}
