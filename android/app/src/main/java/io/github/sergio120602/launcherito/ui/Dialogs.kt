package io.github.sergio120602.launcherito.ui

import android.content.ClipboardManager
import android.content.Context
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import io.github.sergio120602.launcherito.data.SpotifyLibrary

/** Todos los cuadros de diálogo de la app. */
@Composable
fun Dialogs(vm: LauncheritoViewModel) {
    if (vm.spotifyDialogOpen) SpotifyDialog(vm)
    if (vm.deleteRows != null) DeleteDialog(vm)
    if (vm.confirmDelete) {
        AlertDialog(
            onDismissRequest = { vm.confirmDelete = false },
            title = { Text("Borrar canciones") },
            text = { Text(vm.deleteQuestion) },
            confirmButton = {
                Button(onClick = vm::deleteMarked, colors = ButtonDefaults.buttonColors(containerColor = DangerRed)) { Text("Borrar") }
            },
            dismissButton = { TextButton(onClick = { vm.confirmDelete = false }) { Text("Cancelar") } },
        )
    }
    vm.message?.let { text ->
        AlertDialog(
            onDismissRequest = { vm.message = null },
            title = { Text(vm.messageTitle) },
            text = { Text(text) },
            confirmButton = { TextButton(onClick = { vm.message = null }) { Text("Aceptar") } },
        )
    }
}

/** Pegar el enlace de una lista pública o de un álbum de Spotify. */
@Composable
private fun SpotifyDialog(vm: LauncheritoViewModel) {
    val context = LocalContext.current
    // Si se acaba de copiar un enlace de Spotify, ya aparece pegado.
    LaunchedEffect(Unit) {
        if (vm.spotifyLink.isEmpty()) {
            val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            val text = clipboard.primaryClip?.takeIf { it.itemCount > 0 }?.getItemAt(0)?.coerceToText(context)?.toString()
            if (SpotifyLibrary.parseLink(text) != null) vm.spotifyLink = text!!.trim()
        }
    }
    AlertDialog(
        onDismissRequest = vm::closeSpotifyDialog,
        title = { Text("Lista de Spotify") },
        text = {
            Column {
                Text(
                    "Pega el enlace de una lista pública o de un álbum. No hace falta cuenta. Las canciones que " +
                        "ya tienes en .mp3 suenan enteras; las demás, con su vídeo de YouTube.",
                    color = Muted, fontSize = 14.sp,
                )
                Spacer(Modifier.padding(6.dp))
                OutlinedTextField(
                    value = vm.spotifyLink,
                    onValueChange = { vm.spotifyLink = it },
                    singleLine = true,
                    enabled = !vm.spotifyBusy,
                    placeholder = { Text("https://open.spotify.com/playlist/…") },
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
                    keyboardActions = KeyboardActions(onGo = { vm.addSpotifyList() }),
                    modifier = Modifier.fillMaxWidth(),
                )
                if (vm.spotifyBusy) {
                    Spacer(Modifier.padding(4.dp))
                    LinearProgressIndicator(Modifier.fillMaxWidth(), color = SpotifyGreen)
                }
                if (vm.spotifyDialogStatus.isNotEmpty()) {
                    Spacer(Modifier.padding(4.dp))
                    Text(vm.spotifyDialogStatus, fontSize = 13.sp)
                }
            }
        },
        confirmButton = {
            Button(
                onClick = vm::addSpotifyList,
                enabled = !vm.spotifyBusy,
                colors = ButtonDefaults.buttonColors(containerColor = SpotifyGreen, contentColor = Color.Black),
            ) { Text("Añadir") }
        },
        dismissButton = { TextButton(onClick = vm::closeSpotifyDialog, enabled = !vm.spotifyBusy) { Text("Cancelar") } },
    )
}

/** Elegir con casillas qué canciones quitar de «Tus canciones» (los .mp3 del móvil no se tocan). */
@Composable
private fun DeleteDialog(vm: LauncheritoViewModel) {
    val rows = vm.deleteRows ?: return
    val marked = vm.markedForDelete.size
    Dialog(onDismissRequest = vm::closeDeleteDialog, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(
            shape = RoundedCornerShape(20.dp),
            color = Color(0xFF121212),
            modifier = Modifier.fillMaxWidth(0.94f).fillMaxSize(0.88f),
        ) {
            Column(Modifier.padding(vertical = 16.dp)) {
                Text("Borrar canciones", fontSize = 22.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(horizontal = 20.dp))
                Text(
                    "Se quitan de «Tus canciones». Los archivos .mp3 del móvil no se borran.",
                    color = Muted, fontSize = 13.sp, modifier = Modifier.padding(horizontal = 20.dp, vertical = 4.dp),
                )
                TextButton(onClick = vm::toggleSelectAll, modifier = Modifier.padding(horizontal = 8.dp)) {
                    Text(if (marked > 0 && marked == rows.size) "Quitar la selección" else "Seleccionar todas")
                }
                LazyColumn(Modifier.weight(1f)) {
                    items(rows, key = { it.key }) { row ->
                        val checked = vm.deleteChecked[row.key] == true
                        Row(
                            Modifier.fillMaxWidth().clickable { vm.deleteChecked[row.key] = !checked }.padding(horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Checkbox(
                                checked = checked,
                                onCheckedChange = { vm.deleteChecked[row.key] = it },
                                colors = CheckboxDefaults.colors(checkedColor = DangerRed),
                            )
                            Text(
                                buildAnnotatedString {
                                    append(row.title)
                                    if (row.artist.isNotEmpty()) withStyle(SpanStyle(color = Muted)) { append("  ·  " + row.artist) }
                                },
                                maxLines = 1, overflow = TextOverflow.Ellipsis, fontSize = 14.sp, modifier = Modifier.weight(1f),
                            )
                            Spacer(Modifier.width(10.dp))
                            // Marca a la derecha: de dónde viene, o que el archivo ya no está.
                            Text(
                                when {
                                    row.missing -> "no se encuentra"
                                    row.isSpotify -> "Spotify"
                                    else -> ".mp3"
                                },
                                fontSize = 12.sp,
                                color = when {
                                    row.missing -> DangerRed
                                    row.isSpotify -> SpotifyGreen
                                    else -> Muted
                                },
                                modifier = Modifier.padding(end = 12.dp),
                            )
                        }
                    }
                }
                Row(
                    Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.End,
                ) {
                    TextButton(onClick = vm::closeDeleteDialog) { Text("Cancelar") }
                    Spacer(Modifier.width(8.dp))
                    Button(
                        onClick = { vm.confirmDelete = true },
                        enabled = marked > 0,
                        colors = ButtonDefaults.buttonColors(containerColor = DangerRed, contentColor = Color.White),
                    ) { Text(if (marked == 0) "Borrar" else "Borrar ${LauncheritoViewModel.songs(marked)}") }
                }
            }
        }
    }
}
