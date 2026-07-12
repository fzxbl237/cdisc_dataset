namespace P21.Validator.Core.Engine;

public interface ValidationEngine
{
    void Prepare(EngineEntity entity);
    bool Validate();
}
