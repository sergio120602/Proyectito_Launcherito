package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.SystemUpdate
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Al final de la pantalla inicial: la versión instalada y «Buscar actualizaciones» o, si hay una
 * versión nueva, el botón «Actualizar a la X». Debajo, el progreso de la descarga y los avisos.
 */
@Composable
fun UpdateSection(vm: LauncheritoViewModel) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        val update = vm.appUpdate
        if (update == null) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Versión ${vm.appVersion}", color = Muted, fontSize = 13.sp)
                TextButton(onClick = vm::updateButton) {
                    Icon(Icons.Rounded.SystemUpdate, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Buscar actualizaciones", fontSize = 13.sp)
                }
            }
        } else {
            Text("Tienes la ${vm.appVersion}", color = Muted, fontSize = 13.sp)
            Spacer(Modifier.height(6.dp))
            Button(
                onClick = vm::updateButton,
                enabled = vm.updateProgress == null,
                colors = ButtonDefaults.buttonColors(containerColor = Accent, contentColor = Color.White),
            ) {
                Icon(Icons.Rounded.SystemUpdate, contentDescription = null, modifier = Modifier.size(20.dp))
                Spacer(Modifier.width(8.dp))
                Text("Actualizar a la ${update.version}", fontSize = 16.sp)
            }
        }
        vm.updateProgress?.let { progress ->
            Spacer(Modifier.height(8.dp))
            LinearProgressIndicator(progress = { progress }, color = Accent, modifier = Modifier.widthIn(max = 260.dp).fillMaxWidth())
        }
        vm.updateStatus?.let { status ->
            Spacer(Modifier.height(6.dp))
            Text(status, color = Muted, fontSize = 13.sp, textAlign = TextAlign.Center, modifier = Modifier.padding(horizontal = 16.dp))
        }
    }
}

/** Cuadro «¿Actualizar a la X?», desde la pantalla inicial o el menú ⋮. */
@Composable
fun UpdateDialog(vm: LauncheritoViewModel) {
    val update = vm.appUpdate ?: return
    if (!vm.updateConfirm) return
    AlertDialog(
        onDismissRequest = { vm.updateConfirm = false },
        title = { Text("Actualizar a la ${update.version}") },
        text = {
            Text(
                "Tienes la ${vm.appVersion}. Se descarga «${update.title}» de GitHub y se abre el instalador de " +
                    "Android, que te pedirá confirmar. Tus canciones y playlists se quedan como están.\n\n" +
                    "La primera vez Android te pedirá permitir que Launcherito instale apps.",
            )
        },
        confirmButton = {
            Button(
                onClick = vm::installUpdate,
                colors = ButtonDefaults.buttonColors(containerColor = Accent, contentColor = Color.White),
            ) { Text("Actualizar") }
        },
        dismissButton = {
            Column(horizontalAlignment = Alignment.End) {
                TextButton(onClick = { vm.updateConfirm = false; vm.openUpdatePage() }) { Text("Ver en GitHub") }
                TextButton(onClick = { vm.updateConfirm = false }) { Text("Ahora no") }
            }
        },
    )
}
