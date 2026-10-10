package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ArrowBack
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.Person
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil3.compose.AsyncImage

/** Pestañas Artistas y Géneros: las tarjetas o, si se ha pulsado una, sus canciones. */
@Composable
fun CategoryView(vm: LauncheritoViewModel) {
    val open = vm.openCategory
    if (open != null) {
        CategoryDetail(vm, open)
        return
    }
    val artists = vm.section == Section.Artists
    val categories = vm.categories()
    if (categories.isEmpty()) {
        Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
            Text(
                if (artists) "Aún no hay artistas." else "Las canciones aparecerán aquí cuando Deezer las clasifique.",
                color = Muted, textAlign = TextAlign.Center,
            )
        }
        return
    }
    LazyVerticalGrid(
        columns = GridCells.Adaptive(if (artists) 120.dp else 150.dp),
        contentPadding = PaddingValues(16.dp),
        horizontalArrangement = Arrangement.spacedBy(14.dp),
        verticalArrangement = Arrangement.spacedBy(18.dp),
        modifier = Modifier.fillMaxSize(),
    ) {
        items(categories, key = { it.name }) { category ->
            CategoryCard(vm, category, round = artists)
        }
    }
}

/** Tarjeta de un artista (foto redonda) o de un género (foto rectangular) con su nombre y el número de canciones. */
@Composable
private fun CategoryCard(vm: LauncheritoViewModel, category: Category, round: Boolean) {
    // La foto se pide a Deezer la primera vez que se ve la tarjeta.
    LaunchedEffect(category.pictureArtist) { vm.requestArtistPicture(category.pictureArtist) }
    Column(
        Modifier.clip(RoundedCornerShape(14.dp)).clickable { vm.openCategory(category.name) },
        horizontalAlignment = if (round) Alignment.CenterHorizontally else Alignment.Start,
    ) {
        Picture(
            category.picture, round,
            Modifier.fillMaxWidth().aspectRatio(if (round) 1f else 230f / 140f),
        )
        Text(
            category.name, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis,
            textAlign = if (round) TextAlign.Center else TextAlign.Start,
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        )
        Text(
            LauncheritoViewModel.songs(category.count), fontSize = 12.sp, color = Muted,
            textAlign = if (round) TextAlign.Center else TextAlign.Start, modifier = Modifier.fillMaxWidth(),
        )
    }
}

@Composable
private fun Picture(url: String?, round: Boolean, modifier: Modifier) {
    val shape: Shape = if (round) CircleShape else RoundedCornerShape(14.dp)
    Box(modifier.clip(shape).background(EmptyFill), contentAlignment = Alignment.Center) {
        Icon(
            if (round) Icons.Rounded.Person else Icons.Rounded.MusicNote,
            contentDescription = null, tint = PlaceholderColor, modifier = Modifier.size(44.dp),
        )
        if (url != null) {
            AsyncImage(url, contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
        }
    }
}

/** Canciones de un artista o de un género; al pulsar una, suena. */
@Composable
private fun CategoryDetail(vm: LauncheritoViewModel, name: String) {
    val artists = vm.section == Section.Artists
    val songs = vm.categorySongs(name)
    val category = vm.categories().firstOrNull { it.name.equals(name, ignoreCase = true) }
    Column(Modifier.fillMaxSize()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = { vm.openCategory(null) }) {
                Icon(Icons.AutoMirrored.Rounded.ArrowBack, contentDescription = "Volver")
            }
            Picture(
                category?.picture, artists,
                if (artists) Modifier.size(64.dp) else Modifier.width(96.dp).height(60.dp),
            )
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(name, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(
                    (if (artists) "Artista · " else "Género · ") + LauncheritoViewModel.songs(songs.size),
                    color = Muted, fontSize = 13.sp,
                )
            }
        }
        LazyColumn(Modifier.fillMaxSize()) {
            items(songs, key = { it.first }) { (key, meta) ->
                SongRow(vm, key, meta.title, if (artists) meta.genreName else meta.artist)
            }
        }
    }
}
