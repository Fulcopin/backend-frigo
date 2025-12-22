import * as XLSX from 'xlsx';

/**
 * Servicio de Exportación a Excel Dinámico
 * Genera archivos Excel basándose en la estructura del template
 */

/**
 * Exporta un formulario individual a Excel
 * @param {number} formId - ID del formulario a exportar
 */
export const exportFormToExcel = async (formId) => {
  try {
    // Obtener datos completos del formulario
    const response = await fetch(`/api/FilledForms/${formId}/with-template`);
    
    if (!response.ok) {
      throw new Error(`Error al obtener formulario: ${response.status}`);
    }
    
    const formData = await response.json();
    
    // Crear workbook
    const workbook = generateFormWorkbook(formData);
    
    // Descargar
    const fileName = `${formData.template.codigo || 'Formulario'}_${formData.formID}_${formatDate(formData.createdAt)}.xlsx`;
    XLSX.writeFile(workbook, fileName);
    
    return { success: true, fileName };
    
  } catch (error) {
    console.error('Error exportando Excel:', error);
    throw error;
  }
};

/**
 * Exporta múltiples formularios a un solo archivo Excel
 * @param {number[]} formIds - Array de IDs de formularios
 */
export const exportMultipleFormsToExcel = async (formIds) => {
  try {
    const response = await fetch('/api/FilledForms/export-multiple', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(formIds)
    });
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    // Crear workbook con múltiples hojas
    const workbook = XLSX.utils.book_new();
    
    result.forms.forEach((formData, index) => {
      const sheetName = `Form_${formData.formID}`;
      const worksheet = generateFormWorksheet(formData);
      XLSX.utils.book_append_sheet(workbook, worksheet, sheetName);
    });
    
    // Descargar
    const fileName = `Reporte_${result.count}_formularios_${formatDate(new Date())}.xlsx`;
    XLSX.writeFile(workbook, fileName);
    
    return { success: true, fileName, count: result.count };
    
  } catch (error) {
    console.error('Error exportando Excel múltiple:', error);
    throw error;
  }
};

/**
 * Genera el workbook completo de un formulario
 * @param {object} formData - Datos del formulario con template
 * @returns {XLSX.WorkBook}
 */
const generateFormWorkbook = (formData) => {
  const workbook = XLSX.utils.book_new();
  const worksheet = generateFormWorksheet(formData);
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Formulario');
  return workbook;
};

/**
 * Genera la hoja de cálculo de un formulario
 * @param {object} formData - Datos del formulario con template
 * @returns {XLSX.WorkSheet}
 */
const generateFormWorksheet = (formData) => {
  const data = [];
  let currentRow = 0;
  
  // ===== ENCABEZADO DEL DOCUMENTO =====
  data.push([formData.template.nombre || 'Formulario']);
  currentRow++;
  
  data.push([`Código: ${formData.template.codigo || 'N/A'}`, '', `Versión: ${formData.template.version || '1.0'}`]);
  currentRow++;
  
  data.push([`Fecha: ${formatDate(formData.createdAt)}`, '', `ID: #${formData.formID}`]);
  currentRow++;
  
  data.push([]); // Línea vacía
  currentRow++;
  
  // ===== INFORMACIÓN GENERAL (Header Fields) =====
  if (formData.template.structure.headerFields && formData.template.structure.headerFields.length > 0) {
    data.push(['INFORMACIÓN GENERAL']);
    currentRow++;
    
    const headerData = formData.data.header || {};
    
    formData.template.structure.headerFields.forEach(field => {
      const value = headerData[field.label] || '';
      data.push([field.label, value]);
      currentRow++;
    });
    
    data.push([]); // Línea vacía
    currentRow++;
  }
  
  // ===== SECCIONES DEL CUERPO (Body Elements) =====
  if (formData.template.structure.bodyElements && formData.template.structure.bodyElements.length > 0) {
    const bodyData = formData.data.body || [];
    
    formData.template.structure.bodyElements.forEach((element, index) => {
      if (element.type === 'table') {
        // Título de la tabla
        data.push([element.title || 'Tabla']);
        currentRow++;
        
        const columns = element.columns || [];
        const rows = bodyData[index]?.rows || [];
        
        // Encabezados
        const headers = columns.map(col => col.name || col.label || '');
        data.push(headers);
        currentRow++;
        
        // Filas de datos
        rows.forEach(row => {
          const rowData = columns.map(col => {
            const colName = col.name || col.label || col.id;
            return row[colName] || '';
          });
          data.push(rowData);
          currentRow++;
        });
        
        data.push([]); // Línea vacía
        currentRow++;
        
      } else if (element.type === 'text') {
        data.push([element.label || element.title || 'Texto']);
        currentRow++;
        
        const value = bodyData[index]?.value || '';
        data.push([value]);
        currentRow++;
        
        data.push([]); // Línea vacía
        currentRow++;
      }
    });
  }
  
  // ===== OBSERVACIONES =====
  if (formData.observaciones) {
    data.push(['OBSERVACIONES']);
    currentRow++;
    
    data.push([formData.observaciones]);
    currentRow++;
    
    data.push([]); // Línea vacía
    currentRow++;
  }
  
  // ===== FIRMAS =====
  if (formData.template.structure.firmas && formData.template.structure.firmas.length > 0) {
    data.push(['FIRMAS Y APROBACIONES']);
    currentRow++;
    
    const firmasData = formData.data.firmas || {};
    
    formData.template.structure.firmas.forEach(firma => {
      const firmante = firmasData[firma.puesto] || '';
      data.push([firma.puesto, firmante]);
      currentRow++;
    });
    
    data.push([]); // Línea vacía
    currentRow++;
  }
  
  // ===== PIE DE PÁGINA =====
  data.push([`Generado: ${formatDate(new Date())}`]);
  currentRow++;
  
  if (formData.isHistorical) {
    data.push(['NOTA: Este es un formulario histórico']);
    currentRow++;
  }
  
  // Crear worksheet
  const worksheet = XLSX.utils.aoa_to_sheet(data);
  
  // Aplicar estilos (ancho de columnas)
  const wscols = [
    { wch: 30 }, // Columna A - Labels
    { wch: 30 }, // Columna B - Values
    { wch: 20 }, // Columna C
    { wch: 15 }, // Columna D
    { wch: 15 }, // Columna E
    { wch: 15 }  // Columna F
  ];
  worksheet['!cols'] = wscols;
  
  return worksheet;
};

/**
 * Exporta formularios de un template específico a Excel
 */
export const exportTemplateFormsToExcel = async (templateId) => {
  try {
    const response = await fetch(`/api/FilledForms/export-by-template/${templateId}`);
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    if (result.count === 0) {
      throw new Error('No hay formularios para exportar');
    }
    
    return await exportMultipleFormsToExcel(result.forms.map(f => f.formID));
    
  } catch (error) {
    console.error('Error exportando formularios del template:', error);
    throw error;
  }
};

/**
 * Exporta formularios por rango de fechas a Excel
 */
export const exportFormsByDateRangeToExcel = async (startDate, endDate) => {
  try {
    const response = await fetch(
      `/api/FilledForms/export-by-date-range?startDate=${startDate}&endDate=${endDate}`
    );
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    if (result.count === 0) {
      throw new Error('No hay formularios en el rango de fechas especificado');
    }
    
    return await exportMultipleFormsToExcel(result.forms.map(f => f.formID));
    
  } catch (error) {
    console.error('Error exportando formularios por fecha:', error);
    throw error;
  }
};

/**
 * Exporta datos tabulares consolidados de múltiples formularios
 * (Útil para análisis de datos)
 */
export const exportConsolidatedDataToExcel = async (formIds) => {
  try {
    const response = await fetch('/api/FilledForms/export-multiple', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(formIds)
    });
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    // Consolidar todas las tablas del mismo tipo
    const workbook = XLSX.utils.book_new();
    const tablesByName = {};
    
    result.forms.forEach(formData => {
      const bodyElements = formData.template.structure.bodyElements || [];
      const bodyData = formData.data.body || [];
      
      bodyElements.forEach((element, index) => {
        if (element.type === 'table') {
          const tableName = element.title || `Tabla_${index + 1}`;
          
          if (!tablesByName[tableName]) {
            tablesByName[tableName] = {
              columns: element.columns || [],
              rows: []
            };
          }
          
          // Agregar filas con información del formulario
          const rows = bodyData[index]?.rows || [];
          rows.forEach(row => {
            tablesByName[tableName].rows.push({
              FormID: formData.formID,
              Fecha: formatDate(formData.createdAt),
              ...row
            });
          });
        }
      });
    });
    
    // Crear hoja por cada tabla consolidada
    Object.entries(tablesByName).forEach(([tableName, tableData]) => {
      const headers = ['FormID', 'Fecha', ...tableData.columns.map(col => col.name || col.label || '')];
      const rows = tableData.rows.map(row => {
        return [
          row.FormID,
          row.Fecha,
          ...tableData.columns.map(col => {
            const colName = col.name || col.label || col.id;
            return row[colName] || '';
          })
        ];
      });
      
      const data = [headers, ...rows];
      const worksheet = XLSX.utils.aoa_to_sheet(data);
      
      // Ancho de columnas
      const wscols = headers.map(() => ({ wch: 15 }));
      worksheet['!cols'] = wscols;
      
      // Nombre de hoja válido (max 31 caracteres)
      const sheetName = tableName.substring(0, 31);
      XLSX.utils.book_append_sheet(workbook, worksheet, sheetName);
    });
    
    // Descargar
    const fileName = `Datos_Consolidados_${result.count}_formularios_${formatDate(new Date())}.xlsx`;
    XLSX.writeFile(workbook, fileName);
    
    return { success: true, fileName, count: result.count };
    
  } catch (error) {
    console.error('Error exportando datos consolidados:', error);
    throw error;
  }
};

/**
 * Formatea fecha a string legible
 */
const formatDate = (date) => {
  if (!date) return '';
  const d = new Date(date);
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();
  return `${day}/${month}/${year}`;
};

export default {
  exportFormToExcel,
  exportMultipleFormsToExcel,
  exportTemplateFormsToExcel,
  exportFormsByDateRangeToExcel,
  exportConsolidatedDataToExcel
};
