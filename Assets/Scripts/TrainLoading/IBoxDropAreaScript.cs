using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBoxDropArea
{
    // Apakah area ini masih bisa menerima box (satu area hanya untuk satu box)
    bool CanAccept(BoxScript box);
    void OnBoxDrop(BoxScript box);
    void RemoveBox(BoxScript box);
}
