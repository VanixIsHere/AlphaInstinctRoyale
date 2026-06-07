using UnityEngine;
using AIRBattleSimulation;
using AIR.Shared.GameSession;

[DisallowMultipleComponent]
public class UnitInstance : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const float AttackPulseDurationSeconds = 0.18f;
    private const float AttackWindupPresentationDurationSeconds = 0.12f;
    private const float DamagePulseDurationSeconds = 0.24f;
    private const float RetirementDurationSeconds = 0.32f;
    private const float AttackScaleBoost = 0.08f;
    private const float DamageScaleBoost = 0.05f;
    private const float MoveScaleBoost = 0.03f;

    public string RuntimeUnitId { get; private set; } = string.Empty;
    public UnitDataSO unit;
    public int level = 1;
    public int currentHealth;
    public int MaxHealth { get; private set; }
    public string CurrentTargetUnitId { get; private set; } = string.Empty;
    public BattleUnitActionType CurrentActionType { get; private set; } = BattleUnitActionType.Idle;
    public string CurrentActionTargetUnitId { get; private set; } = string.Empty;
    public string CurrentActionOriginTileId { get; private set; } = string.Empty;
    public string CurrentActionDestinationTileId { get; private set; } = string.Empty;
    public float CurrentActionProgress01 { get; private set; }
    public float CurrentActionExecutionProgress01 { get; private set; }
    public bool CurrentActionHasExecuted { get; private set; }
    public int LastAttackResolvedTick { get; private set; } = -1;
    public int LastDamageTakenTick { get; private set; } = -1;
    public int LastDamageAmount { get; private set; }
    public BattleUnitActionType LastResolvedActionType { get; private set; } = BattleUnitActionType.Idle;
    public int LastResolvedActionTick { get; private set; } = -1;
    public bool IsMoving { get; private set; }
    public bool IsRetiring => retirementStarted;

    public BenchManager OwningBench { get; private set; }
    public int BenchSlotIndex { get; private set; } = -1;
    public Transform BenchSlotTransform { get; private set; }
    public string FieldTileId { get; private set; } = string.Empty;
    public Transform FieldTileTransform { get; private set; }
    public UnitContainerType ContainerType { get; private set; } = UnitContainerType.Bench;

    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private Vector3 baseScale;
    private float attackPulseTimer;
    private float attackWindupPresentationTimer;
    private float damagePulseTimer;
    private bool visualsDirty = true;
    private bool hasFacingTarget;
    private Vector3 facingTargetPosition;
    private float retirementTimer;
    private bool retirementStarted;
    private bool interactionsEnabled = true;

    private void Awake()
    {
        CacheVisualComponents();
    }

    private void LateUpdate()
    {
        if (attackPulseTimer > 0f)
        {
            attackPulseTimer = Mathf.Max(0f, attackPulseTimer - Time.deltaTime);
            visualsDirty = true;
        }

        if (attackWindupPresentationTimer > 0f)
        {
            attackWindupPresentationTimer = Mathf.Max(0f, attackWindupPresentationTimer - Time.deltaTime);
            visualsDirty = true;
        }

        if (damagePulseTimer > 0f)
        {
            damagePulseTimer = Mathf.Max(0f, damagePulseTimer - Time.deltaTime);
            visualsDirty = true;
        }

        if (retirementTimer > 0f)
        {
            retirementTimer = Mathf.Max(0f, retirementTimer - Time.deltaTime);
            visualsDirty = true;
        }

        UpdateFacing();
        ApplyVisualFeedback();
    }

    public void Init(UnitDataSO data, int level = 1, int? currentHealthOverride = null, string runtimeUnitId = "")
    {
        CacheVisualComponents();
        RuntimeUnitId = runtimeUnitId;
        unit = data;
        this.level = level;
        MaxHealth = GetBaseMaxHealth();
        currentHealth = currentHealthOverride ?? MaxHealth;
        name = $"{unit.Data.UnitName} (Lvl {level})";
        retirementStarted = false;
        retirementTimer = 0f;
        visualsDirty = true;
    }

    public int GetMaxHealth()
    {
        return MaxHealth;
    }

    public int GetBaseMaxHealth()
    {
        return unit.Data.BaseHealth * level;
    }

    public int GetAttack()
    {
        return unit.Data.BaseAttack * level;
    }

    public void BindToBench(BenchManager bench, int slotIndex, Transform slotTransform)
    {
        OwningBench = bench;
        BenchSlotIndex = slotIndex;
        BenchSlotTransform = slotTransform;
        FieldTileId = string.Empty;
        FieldTileTransform = null;
        ContainerType = UnitContainerType.Bench;
        hasFacingTarget = false;
        visualsDirty = true;
    }

    public void BindToField(string tileId, Transform tileTransform)
    {
        OwningBench = null;
        BenchSlotIndex = -1;
        BenchSlotTransform = null;
        FieldTileId = tileId;
        FieldTileTransform = tileTransform;
        ContainerType = UnitContainerType.Field;
        visualsDirty = true;
    }

    public void UpdateRuntimeState(int newLevel, int newCurrentHealth, int newMaxHealth, string runtimeUnitId = "")
    {
        level = newLevel;
        currentHealth = newCurrentHealth;
        MaxHealth = newMaxHealth > 0 ? newMaxHealth : GetBaseMaxHealth();

        if (!string.IsNullOrWhiteSpace(runtimeUnitId))
        {
            RuntimeUnitId = runtimeUnitId;
        }

        if (unit != null)
        {
            name = $"{unit.Data.UnitName} (Lvl {level})";
        }

        visualsDirty = true;
    }

    public void UpdateRuntimeState(UnitRuntimeState runtimeState, int snapshotTick)
    {
        if (runtimeState == null)
        {
            return;
        }

        int previousLastResolvedActionTick = LastResolvedActionTick;

        UpdateRuntimeState(runtimeState.Level, runtimeState.CurrentHealth, runtimeState.MaxHealth, runtimeState.RuntimeUnitId);
        CurrentActionType = runtimeState.CurrentActionType;
        CurrentActionTargetUnitId = runtimeState.CurrentActionTargetUnitId ?? string.Empty;
        CurrentActionOriginTileId = runtimeState.CurrentActionOriginTileId ?? string.Empty;
        CurrentActionDestinationTileId = runtimeState.CurrentActionDestinationTileId ?? string.Empty;
        CurrentActionProgress01 = Mathf.Clamp01(runtimeState.CurrentActionProgress01);
        CurrentActionExecutionProgress01 = Mathf.Clamp01(runtimeState.CurrentActionExecutionProgress01);
        CurrentActionHasExecuted = runtimeState.CurrentActionHasExecuted;
        LastResolvedActionType = runtimeState.LastResolvedActionType;
        LastResolvedActionTick = runtimeState.LastResolvedActionTick;
        CurrentTargetUnitId = !string.IsNullOrWhiteSpace(CurrentActionTargetUnitId)
            ? CurrentActionTargetUnitId
            : runtimeState.CurrentTargetUnitId ?? string.Empty;
        IsMoving = CurrentActionType == BattleUnitActionType.Moving || runtimeState.IsMoving;
        LastDamageAmount = runtimeState.LastDamageAmount;

        if (runtimeState.LastResolvedActionType == BattleUnitActionType.Attacking &&
            runtimeState.LastResolvedActionTick > previousLastResolvedActionTick &&
            runtimeState.LastResolvedActionTick == snapshotTick)
        {
            attackPulseTimer = AttackPulseDurationSeconds;
        }

        if (runtimeState.LastDamageTakenTick > LastDamageTakenTick && runtimeState.LastDamageTakenTick == snapshotTick)
        {
            damagePulseTimer = DamagePulseDurationSeconds;
        }

        LastAttackResolvedTick = runtimeState.LastAttackResolvedTick;
        LastDamageTakenTick = runtimeState.LastDamageTakenTick;
        visualsDirty = true;
    }

    public void SetBattleTarget(Vector3 targetPosition)
    {
        facingTargetPosition = targetPosition;
        hasFacingTarget = true;
    }

    public void ClearBattleTarget()
    {
        hasFacingTarget = false;
    }

    public void BeginRetirement()
    {
        if (retirementStarted)
        {
            return;
        }

        retirementStarted = true;
        retirementTimer = RetirementDurationSeconds;
        DisableInteractionComponents();
        visualsDirty = true;
    }

    public void NotifyAttackWindup()
    {
        attackWindupPresentationTimer = AttackWindupPresentationDurationSeconds;
        visualsDirty = true;
    }

    public bool IsRetirementComplete()
    {
        return retirementStarted && retirementTimer <= 0f;
    }

    private void CacheVisualComponents()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        propertyBlock ??= new MaterialPropertyBlock();
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }
    }

    private void UpdateFacing()
    {
        if (ContainerType != UnitContainerType.Field || !hasFacingTarget)
        {
            return;
        }

        Vector3 lookDirection = facingTargetPosition - transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
            Mathf.Clamp01(Time.deltaTime * 12f));
    }

    private void ApplyVisualFeedback()
    {
        if (!visualsDirty)
        {
            return;
        }

        float attackPulse = attackPulseTimer > 0f
            ? 1f - (attackPulseTimer / AttackPulseDurationSeconds)
            : 1f;
        float damagePulse = damagePulseTimer > 0f
            ? 1f - (damagePulseTimer / DamagePulseDurationSeconds)
            : 1f;
        float transientAttackWindupEnvelope = attackWindupPresentationTimer > 0f
            ? Mathf.Sin((1f - (attackWindupPresentationTimer / AttackWindupPresentationDurationSeconds)) * (Mathf.PI * 0.5f))
            : 0f;

        float authoritativeAttackEnvelope = CurrentActionType == BattleUnitActionType.Attacking && !CurrentActionHasExecuted
            ? Mathf.Sin(Mathf.Clamp01(CurrentActionExecutionProgress01) * (Mathf.PI * 0.5f))
            : 0f;
        float movementEnvelope = CurrentActionType == BattleUnitActionType.Moving
            ? Mathf.Sin(Mathf.Clamp01(CurrentActionProgress01) * Mathf.PI)
            : 0f;
        float transientAttackEnvelope = attackPulseTimer > 0f ? Mathf.Sin(attackPulse * Mathf.PI) : 0f;
        float attackEnvelope = Mathf.Max(Mathf.Max(authoritativeAttackEnvelope, transientAttackEnvelope), transientAttackWindupEnvelope);
        float damageEnvelope = damagePulseTimer > 0f ? Mathf.Sin(damagePulse * Mathf.PI) : 0f;
        float retirementProgress = retirementTimer > 0f
            ? 1f - (retirementTimer / RetirementDurationSeconds)
            : 0f;
        float retirementScale = retirementTimer > 0f ? Mathf.Lerp(1f, 0.7f, retirementProgress) : 1f;
        float scaleMultiplier = retirementScale * (1f + (movementEnvelope * MoveScaleBoost) + (attackEnvelope * AttackScaleBoost) + (damageEnvelope * DamageScaleBoost));
        transform.localScale = baseScale * scaleMultiplier;

        Color tint = Color.white;
        if (movementEnvelope > 0f)
        {
            tint = Color.Lerp(tint, new Color(0.76f, 0.9f, 1f, 1f), movementEnvelope * 0.22f);
        }

        if (attackEnvelope > 0f)
        {
            tint = Color.Lerp(tint, new Color(1f, 0.9f, 0.45f, 1f), attackEnvelope * 0.55f);
        }

        if (damageEnvelope > 0f)
        {
            tint = Color.Lerp(tint, new Color(1f, 0.38f, 0.38f, 1f), damageEnvelope * 0.8f);
        }

        if (retirementTimer > 0f)
        {
            tint = Color.Lerp(tint, new Color(0.35f, 0.35f, 0.35f, 0.35f), retirementProgress);
        }

        ApplyTint(tint);
        visualsDirty = attackPulseTimer > 0f || attackWindupPresentationTimer > 0f || damagePulseTimer > 0f || retirementTimer > 0f;
    }

    private void ApplyTint(Color tint)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return;
        }

        foreach (Renderer cachedRenderer in cachedRenderers)
        {
            if (cachedRenderer == null)
            {
                continue;
            }

            cachedRenderer.GetPropertyBlock(propertyBlock);
            Material sharedMaterial = cachedRenderer.sharedMaterial;
            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, tint);
                }

                if (sharedMaterial.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, tint);
                }
            }

            cachedRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void DisableInteractionComponents()
    {
        if (!interactionsEnabled)
        {
            return;
        }

        UnitDragInteraction dragInteraction = GetComponent<UnitDragInteraction>();
        if (dragInteraction != null)
        {
            dragInteraction.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        interactionsEnabled = false;
    }
}
