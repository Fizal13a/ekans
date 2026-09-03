public enum BossAttackType
{
    LightAttack,
    MidAttack,
    SpecialAttack
}

public interface IChefAttack
{
    BossAttackType AttackType { get; }
    void StartAttack();
}