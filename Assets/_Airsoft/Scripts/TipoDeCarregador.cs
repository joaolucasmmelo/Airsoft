namespace Airsoft
{
    /// <summary>
    /// Tipo usado para saber se um carregador serve numa arma.
    /// A arma e o carregador usam este MESMO enum (nada de comparar textos).
    /// As 4 primeiras opções são as pedidas no enunciado; Sniper foi adicionada para a terceira arma.
    /// </summary>
    public enum TipoDeCarregador
    {
        Shotgun,
        Rifle,
        Pistol1911,
        PistolGlock,
        Sniper
    }
}
